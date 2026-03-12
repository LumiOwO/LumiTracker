import cv2
import numpy as np
import time
import json
import os
import argparse
import random
from pathlib import Path
from collections import defaultdict
from sklearn.metrics import precision_score, recall_score, f1_score

from .config import cfg, LogInfo, LogWarning, LogError, LogDebug
from .database import Database, LoadImage, DatabaseUpdateContext
from .enums import EAnnType, EActionCard
from .feature import ActionCardHandler, ExtractFeature_ActionCard, FeatureDistance, ImageHash, CropBox

class ImageAugmentor:
    @staticmethod
    def apply_random_brightness_contrast(image, alpha_range=(0.7, 1.3), beta_range=(-40, 40)):
        alpha = random.uniform(*alpha_range) # Contrast
        beta = random.uniform(*beta_range)   # Brightness
        adjusted = cv2.convertScaleAbs(image, alpha=alpha, beta=beta)
        return adjusted

    @staticmethod
    def apply_random_blur(image, kernel_sizes=[(5, 5), (7, 7)]):
        ksize = random.choice(kernel_sizes)
        return cv2.GaussianBlur(image, ksize, 0)
        
    @staticmethod
    def apply_random_noise(image, noise_type="gaussian"):
        if noise_type == "gaussian":
            row, col, ch = image.shape
            mean = 0
            var = 50
            sigma = var**0.5
            gauss = np.random.normal(mean, sigma, (row, col, ch))
            gauss = gauss.reshape(row, col, ch)
            noisy = image + gauss
            return np.clip(noisy, 0, 255).astype(np.uint8)
        return image

class Benchmark:
    def __init__(self, output_dir="./agent/temp", tag="default"):
        self.output_dir = output_dir
        self.tag = tag
        os.makedirs(self.output_dir, exist_ok=True)
        
        # We need to build a pure database without golden cards for testing
        print(f"Building pure testing database [{self.tag}] without golden cards...")
        # Temporarily override cfg.database_dir
        original_db_dir = cfg.database_dir
        cfg.database_dir = self.output_dir
        
        ctx = DatabaseUpdateContext()
        ctx.exclude_golden_cards = True
        self.db = Database()
        self.db._Update(ctx) # This builds the pure db in output_dir
        self.db.Load() # Load the newly built db from output_dir
        
        # Restore original cfg
        cfg.database_dir = original_db_dir
        
        self.handler = ActionCardHandler()
        self.handler.OnResize(CropBox(0, 0, 420, 720))
        self.results = {}
        
    def load_dataset(self, num_samples=None):
        # We will test on a subset of the action cards
        action_cards_dir = os.path.join(cfg.cards_dir, "actions")
        all_actions = self.db["actions"]
        
        dataset = []
        count = 0
        
        # Load sharables and tokens
        for card_id, action in enumerate(all_actions):
            if action is None:
                continue
                
            name = action.get("zh-HANS", "unknown")
            # Simplified share_to_internal logic handling based on database dict
            internal_cutoff = EActionCard.NumSharables.value
            
            if card_id < internal_cutoff: # We will simply check if the file exists as action_ or token_
                image_file = f'action_{card_id}_{name}.png'
                path = os.path.join(action_cards_dir, image_file)
                if not os.path.exists(path):
                    # Try token
                    path = os.path.join(action_cards_dir, "tokens", f"token_{card_id - internal_cutoff}_{name}.png")
            else:
                path = os.path.join(action_cards_dir, "tokens", f"token_{card_id - internal_cutoff}_{name}.png")
                
            if os.path.exists(path):
                image = LoadImage(path)
                if image is not None:
                    # Convert to RGBA if needed
                    if len(image.shape) == 3 and image.shape[-1] == 3:
                        image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
                    dataset.append({
                        "id": card_id,
                        "name": name,
                        "image": image,
                        "is_golden": False
                    })
                    count += 1
                    
            if num_samples and count >= num_samples:
                break
                
        # Load golden cards (edge cases)
        extra_cards_dir = os.path.join(action_cards_dir, "extras")
        if os.path.exists(extra_cards_dir):
            extra_image_names = os.listdir(extra_cards_dir)
            for extra_image_name in extra_image_names:
                info = extra_image_name[:-4] # remove ".png"
                parts = info.split('_')
                if len(parts) >= 4:
                    extra_id  = int(parts[1])
                    mapped_id = int(parts[3])
                    
                    extra_path = os.path.join(extra_cards_dir, extra_image_name)
                    image = LoadImage(extra_path)
                    if image is not None:
                        if len(image.shape) == 3 and image.shape[-1] == 3:
                            image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
                            
                        # Use mapped_id as the true label
                        dataset.append({
                            "id": mapped_id,
                            "name": f"Golden_{mapped_id}",
                            "image": image,
                            "is_golden": True
                        })
                        
        print(f"Loaded {len(dataset)} base images for benchmark.")
        return dataset

    def run_inter_class_benchmark(self, dataset):
        print("Running Inter-class distance benchmark...")
        # Extract features for all base images
        features = []
        times = []
        
        for item in dataset:
            start_time = time.perf_counter()
            self.handler.frame_buffer = item["image"]
            ahash, dhash = self.handler.ExtractCardFeatures()
            end_time = time.perf_counter()
            times.append((end_time - start_time) * 1000) # in ms
            
            features.append({
                "id": item["id"],
                "ahash": ahash,
                "dhash": dhash,
                "is_golden": item["is_golden"]
            })
            
        avg_time = sum(times) / len(times) if times else 0
        max_time = max(times) if times else 0
        self.results["avg_extraction_time_ms"] = avg_time
        self.results["max_extraction_time_ms"] = max_time
        print(f"Avg extraction time: {avg_time:.2f} ms")
        print(f"Max extraction time: {max_time:.2f} ms")
        
        # Calculate min inter-class distance
        min_dist_a = float('inf')
        min_dist_d = float('inf')
        
        for i in range(len(features)):
            for j in range(i+1, len(features)):
                if features[i]["id"] == features[j]["id"]:
                    continue # Skip same class
                    
                dist_a = FeatureDistance(features[i]["ahash"], features[j]["ahash"])
                dist_d = FeatureDistance(features[i]["dhash"], features[j]["dhash"])
                
                min_dist_a = min(min_dist_a, dist_a)
                min_dist_d = min(min_dist_d, dist_d)
                
        self.results["min_inter_class_dist_a"] = min_dist_a
        self.results["min_inter_class_dist_d"] = min_dist_d
        
        return features

    def run_intra_class_benchmark(self, dataset):
        print("Running Intra-class (robustness) benchmark...")
        max_dist_a = 0
        max_dist_d = 0
        
        y_true = []
        y_pred_a = []
        y_pred_d = []
        
        # Create augmented versions and test them
        for item in dataset:
            if item["is_golden"]:
                continue # We'll test golden cards separately
                
            base_image = item["image"]
            true_id = item["id"]
            
            # Base features
            self.handler.frame_buffer = base_image
            base_ahash, base_dhash = self.handler.ExtractCardFeatures()
            
            augmentations = [
                ("brightness", lambda img: ImageAugmentor.apply_random_brightness_contrast(img)),
                ("blur", lambda img: ImageAugmentor.apply_random_blur(img)),
                ("noise", lambda img: ImageAugmentor.apply_random_noise(img))
            ]
            
            for aug_name, aug_func in augmentations:
                aug_img = aug_func(base_image)
                self.handler.frame_buffer = aug_img
                aug_ahash, aug_dhash = self.handler.ExtractCardFeatures()
                
                # Intra-class distance (between base and its augmented version)
                dist_a = FeatureDistance(base_ahash, aug_ahash)
                dist_d = FeatureDistance(base_dhash, aug_dhash)
                
                max_dist_a = max(max_dist_a, dist_a)
                max_dist_d = max(max_dist_d, dist_d)
                
                # Test against Database to get accuracy metrics
                pred_ids_a, _ = self.db.SearchByFeature(aug_ahash, EAnnType.ACTIONS_A)
                pred_ids_d, _ = self.db.SearchByFeature(aug_dhash, EAnnType.ACTIONS_D)
                
                y_true.append(true_id)
                pred_a = pred_ids_a[0] if pred_ids_a else -1
                pred_d = pred_ids_d[0] if pred_ids_d else -1
                y_pred_a.append(pred_a)
                y_pred_d.append(pred_d)
                
                # Debug output for mismatched predictions
                # if pred_a != true_id or pred_d != true_id:
                #    print(f"Mismatch! True: {true_id} ({item['name']}), Pred A: {pred_a}, Pred D: {pred_d}")

        self.results["max_intra_class_dist_a"] = max_dist_a
        self.results["max_intra_class_dist_d"] = max_dist_d
        
        # Calculate separation margins
        self.results["separation_margin_a"] = self.results["min_inter_class_dist_a"] - max_dist_a
        self.results["separation_margin_d"] = self.results["min_inter_class_dist_d"] - max_dist_d
        
        # Calculate statistical metrics using sklearn
        # Use macro avg since some classes might not appear often
        self.results["precision_a"] = float(precision_score(y_true, y_pred_a, average='macro', zero_division=0.0))  # type: ignore
        self.results["recall_a"] = float(recall_score(y_true, y_pred_a, average='macro', zero_division=0.0))  # type: ignore
        self.results["f1_a"] = float(f1_score(y_true, y_pred_a, average='macro', zero_division=0.0))  # type: ignore
        
        self.results["precision_d"] = float(precision_score(y_true, y_pred_d, average='macro', zero_division=0.0))  # type: ignore
        self.results["recall_d"] = float(recall_score(y_true, y_pred_d, average='macro', zero_division=0.0))  # type: ignore
        self.results["f1_d"] = float(f1_score(y_true, y_pred_d, average='macro', zero_division=0.0))  # type: ignore
        
        # Simple top-1 accuracy
        acc_a = sum(1 for t, p in zip(y_true, y_pred_a) if t == p) / len(y_true) if y_true else 0
        acc_d = sum(1 for t, p in zip(y_true, y_pred_d) if t == p) / len(y_true) if y_true else 0
        self.results["top1_accuracy_a"] = float(acc_a)
        self.results["top1_accuracy_d"] = float(acc_d)

    def run_edge_cases(self, dataset):
        print("Running Edge Cases (Golden Cards)...")
        golden_cards = [item for item in dataset if item["is_golden"]]
        
        if not golden_cards:
            print("No golden cards found. Skipping.")
            return
            
        success_count = 0
        total_dist_a = 0
        total_dist_d = 0
        
        for item in golden_cards:
            true_id = item["id"]
            self.handler.frame_buffer = item["image"]
            
            # Use the full Update pipeline
            card_id, dist, dists_info = self.handler.Update(
                item["image"], 
                self.db, 
                check_next_dist=True
            )
            
            # Get actual distances to true ID
            ahash, dhash = self.handler.ExtractCardFeatures()
            
            # Search database for true ID vector to get exact distance if not top-1
            # Or just calculate distance to the true ID's base feature
            # Since we rebuilt the DB, we can get the feature by ID
            true_feat_a = self.db.GetFeatureById(true_id, EAnnType.ACTIONS_A)
            true_feat_d = self.db.GetFeatureById(true_id, EAnnType.ACTIONS_D)
            
            dist_a = FeatureDistance(ahash, true_feat_a)
            dist_d = FeatureDistance(dhash, true_feat_d)
            
            total_dist_a += dist_a
            total_dist_d += dist_d
            
            if card_id == true_id:
                success_count += 1
                
        self.results["edge_case_accuracy"] = float(success_count / len(golden_cards))
        self.results["edge_case_avg_dist_a"] = float(total_dist_a / len(golden_cards))
        self.results["edge_case_avg_dist_d"] = float(total_dist_d / len(golden_cards))
        self.results["edge_cases_tested"] = len(golden_cards)

    def save_results(self):
        filename = f"benchmark_{self.tag}.json"
        output_file = os.path.join(self.output_dir, filename)
        
        # Structure results into General and Implementation metrics
        # Implementation metrics are algorithm-specific (e.g. hashing)
        # General metrics are algorithm-agnostic
        
        # Determine top-level metrics based on the best hash component for now
        # In the future, this can be the overall algorithm result
        general = {
            "separation_margin": max(self.results.get("separation_margin_a", -100), self.results.get("separation_margin_d", -100)),
            "top1_accuracy": max(self.results.get("top1_accuracy_a", 0), self.results.get("top1_accuracy_d", 0)),
            "precision": max(self.results.get("precision_a", 0), self.results.get("precision_d", 0)),
            "recall": max(self.results.get("recall_a", 0), self.results.get("recall_d", 0)),
            "f1_score": max(self.results.get("f1_a", 0), self.results.get("f1_d", 0)),
            "edge_case_accuracy": self.results.get("edge_case_accuracy", 0),
            "edge_case_avg_dist": min(self.results.get("edge_case_avg_dist_a", 100), self.results.get("edge_case_avg_dist_d", 100)),
            "avg_extraction_time_ms": self.results.get("avg_extraction_time_ms", 0),
            "max_extraction_time_ms": self.results.get("max_extraction_time_ms", 0)
        }
        
        output_data = {
            "general_metrics": general,
            "implementation_metrics": self.results
        }
        
        with open(output_file, 'w') as f:
            json.dump(output_data, f, indent=4)
        print(f"Results saved to {output_file}")
        
    def run_all(self, num_samples=None):
        dataset = self.load_dataset(num_samples=num_samples)
        self.run_inter_class_benchmark(dataset)
        self.run_intra_class_benchmark(dataset)
        self.run_edge_cases(dataset)
        self.save_results()
        
        print("\n--- Benchmark Summary ---")
        for k, v in self.results.items():
            if isinstance(v, float):
                print(f"{k}: {v:.4f}")
            else:
                print(f"{k}: {v}")
                
        # Agent auto-loop feedback
        if self.results.get("separation_margin_a", 0) > 0 and self.results.get("separation_margin_d", 0) > 0:
            print("\nRESULT: SUCCESS - Positive separation margin achieved.")
        else:
            print("\nRESULT: NEEDS_IMPROVEMENT - Negative separation margin detected. Collisions possible.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="LumiTracker Feature Benchmark")
    parser.add_argument("--samples", type=int, default=None, help="Number of samples to test (for quick runs, defaults to all)")
    parser.add_argument("--output-dir", type=str, default="./agent/temp", help="Directory to save the temp database and benchmark results")
    parser.add_argument("--tag", type=str, default="default", help="Tag for the benchmark run (used in output filename)")
    args = parser.parse_args()
    
    benchmark = Benchmark(output_dir=args.output_dir, tag=args.tag)
    benchmark.run_all(num_samples=args.samples)
