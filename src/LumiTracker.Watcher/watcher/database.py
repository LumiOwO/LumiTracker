from annoy import AnnoyIndex
from PIL import Image
import numpy as np
import imagehash

import logging
import time
import json
import os
from pathlib import Path

from .config import cfg

# Only accept PIL Image with RGB format
def ExtractFeature(image: Image):
    # use dhash + ahash for better robustness
    feature1 = imagehash.dhash(image, hash_size=cfg.hash_size)
    feature1 = feature1.hash.flatten()

    feature2 = imagehash.average_hash(image, hash_size=cfg.hash_size)
    feature2 = feature2.hash.flatten()

    feature = np.append(feature1, feature2)

    return feature

def FeatureDistance(feature1, feature2):
    return imagehash.ImageHash(feature1) - imagehash.ImageHash(feature2)

def HashToFeature(hash_str):
    binary_string = bin(int(hash_str, 16))[2:]
    bool_list = [bit == '1' for bit in binary_string]
    feature = np.array(bool_list, dtype=bool)
    return feature

class Database:
    def __init__(self):
        self.data       = {}
        self.events_ann = None

        Path(cfg.database_dir).mkdir(parents=True, exist_ok=True)
        if cfg.DEBUG:
            Path(cfg.debug_dir).mkdir(parents=True, exist_ok=True)

    def __getitem__(self, key):
        return self.data[key]

    def __setitem__(self, key, value):
        self.data[key] = value
    
    def UpdateControls(self):
        controls = {}

        start_filename = "start.png"
        start = Image.open(os.path.join(cfg.cards_dir, start_filename)).convert("RGBA")
        feature = ExtractFeature(start.convert("RGB"))
        hash_str = str(imagehash.ImageHash(feature))
        controls["start_hash"] = hash_str

        self.data["controls"] = controls

    def UpdateEventCards(self):
        event_cards_dir = os.path.join(cfg.cards_dir, "events")
        files = os.listdir(event_cards_dir)
        image_files = [file for file in files if file.lower().endswith(".png")]

        border_filename = "tcg_border_bg.png"
        border = Image.open(os.path.join(cfg.cards_dir, border_filename)).convert("RGBA")

        n_images = len(image_files)
        events   = [None] * n_images
        features = [None] * n_images
        for image_file in image_files:
            image_path = os.path.join(event_cards_dir, image_file)
            image = Image.open(image_path)
            if image is not None:
                # add border
                image = image.convert("RGBA")
                image = Image.alpha_composite(image, border)
                
                feature = ExtractFeature(image.convert("RGB"))

                infos = image_file.split("_")
                card_id = int(infos[1])
                features[card_id] = feature
                events[card_id] = {
                    "id"        : card_id,
                    "type"      : infos[0],
                    "filename"  : image_file,
                    "name_CN"   : infos[2].removesuffix('.png')
                }
            else:
                logging.error(f"Failed to load image: {image_path}")

        logging.info(f"Loaded {len(features)} images from {event_cards_dir}")
        self.data["events"] = events

        if cfg.DEBUG:
            image_file = events[211]["filename"]
            image = Image.open(os.path.join(event_cards_dir, image_file))
            image = Image.alpha_composite(image, border)
            image.save(os.path.join(cfg.debug_dir, image_file))
            logging.debug(f"save {image_file} at {cfg.debug_dir}")

        ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        for i in range(len(features)):
            ann.add_item(i, features[i])
        ann.build(cfg.ann_n_trees)
        ann.save(os.path.join(cfg.database_dir, cfg.events_ann_filename))
        self.events_ann = ann

        if cfg.DEBUG:
            test_dir = os.path.join(cfg.debug_dir, "test")
            files = os.listdir(test_dir)
            image_files = [file for file in files if file.lower().endswith(".png")]
            image_files = sorted(image_files)
            for file in image_files:
                image = Image.open(os.path.join(test_dir, file)).convert("RGBA")
                begin_time = time.time()

                my_feature = ExtractFeature(image.convert("RGB"))
                my_ids, my_dists = ann.get_nns_by_vector(my_feature, n=10, include_distances=True)

                dt = time.time() - begin_time
                logging.debug(dt)
                # logging.debug(my_ids)
                logging.debug(my_dists)
                found_name = events[my_ids[0]]['name_CN'] if my_dists[0] <= cfg.threshold else "None"
                logging.debug(f"{file}: {found_name}")

            # save last one
            for i, card_id in enumerate(my_ids):
                image = Image.open(os.path.join(event_cards_dir, events[card_id]["filename"]))
                image.save(os.path.join(cfg.debug_dir, f"found{i}.png"))


    def Update(self):
        self.UpdateControls()
        self.UpdateEventCards()

        with open(os.path.join(cfg.database_dir, cfg.db_filename), 'w', encoding='utf-8') as f:
            json.dump(self.data, f, indent=2, ensure_ascii=False)
    
    def Load(self):
        self.events_ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        self.events_ann.load(os.path.join(cfg.database_dir, cfg.events_ann_filename))

        with open(os.path.join(cfg.database_dir, cfg.db_filename), encoding='utf-8') as f:
            self.data = json.load(f)
        
    def SearchByFeature(self, feature, card_type):
        if card_type == "event":
            ann = self.events_ann
    
        ids, dists = ann.get_nns_by_vector(feature, n=1, include_distances=True)
        return ids[0], dists[0]


if __name__ == '__main__':
    db = Database()
    db.Update()