from annoy import AnnoyIndex
from PIL import Image
import numpy as np
import imagehash

import logging
import time
import json
import csv
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

def ExtractFeatureAHash(image: Image):
    feature = imagehash.average_hash(image, hash_size=cfg.hash_size)
    feature = feature.hash.flatten()
    return feature

def FeatureDistance(feature1, feature2):
    return imagehash.ImageHash(feature1) - imagehash.ImageHash(feature2)

def HashToFeature(hash_str):
    binary_string = bin(int(hash_str, 16))[2:]
    expected_length = len(hash_str) * 4
    binary_string = binary_string.zfill(expected_length)
    bool_list = [bit == '1' for bit in binary_string]
    feature = np.array(bool_list, dtype=bool)
    return feature

class Database:
    def __init__(self):
        self.data       = {}
        self.events_ann = None
        self.rounds_ann = None

        Path(cfg.database_dir).mkdir(parents=True, exist_ok=True)
        if cfg.DEBUG:
            Path(cfg.debug_dir).mkdir(parents=True, exist_ok=True)

    def __getitem__(self, key):
        return self.data[key]

    def __setitem__(self, key, value):
        self.data[key] = value
    
    def _UpdateControls(self):
        controls = {}

        start = Image.open(os.path.join(cfg.cards_dir, "controls", "control_GameStart.png")).convert("RGBA")
        start_feature = ExtractFeature(start.convert("RGB"))
        controls["GameStart"] = str(imagehash.ImageHash(start_feature))

        game_round = Image.open(os.path.join(cfg.cards_dir, "controls", "control_Round.png")).convert("RGBA")
        round_feature = ExtractFeatureAHash(game_round.convert("RGB"))
        controls["GameRound"] = str(imagehash.ImageHash(round_feature))
        if cfg.DEBUG:
            n_rounds = 14
            for i in range(n_rounds):
                round_image = Image.open(os.path.join(cfg.debug_dir, f"crop{i + 1}.png")).convert("RGBA")
                feature = ExtractFeatureAHash(round_image.convert("RGB"))
                dist = FeatureDistance(feature, round_feature)
                logging.debug(f'"info": "round{i + 1}, {dist=}"')

        self.data["controls"] = controls

    def _UpdateEventCards(self):
        with open(os.path.join(cfg.cards_dir, "events.csv"), 
                    mode='r', newline='', encoding='utf-8') as csv_file:
            csv_reader = csv.DictReader(csv_file)
            csv_data = [row for row in csv_reader]

        border_filename = "tcg_border_bg.png"
        border = Image.open(os.path.join(cfg.cards_dir, border_filename)).convert("RGBA")

        event_cards_dir = os.path.join(cfg.cards_dir, "events")
        n_images = len(csv_data)
        events   = [None] * n_images
        features = [None] * n_images
        for row in csv_data:
            card_id = int(row["id"])
            image_file = f'event_{card_id}_{row["zh-HANS"]}.png'

            image_path = os.path.join(event_cards_dir, image_file)
            image = Image.open(image_path)
            if image is not None:
                # add border
                image = image.convert("RGBA")
                image = Image.alpha_composite(image, border)
                
                feature = ExtractFeature(image.convert("RGB"))

                features[card_id] = feature
                events[card_id]   = row

                # create snapshot
                top    = int(row["snapshot_top"])
                left   = 12
                height = 150
                crop_box = (left, top, 420 - left, top + height)
                snapshot = image.crop(crop_box).convert("RGB")
                snapshot_path = os.path.join(
                    cfg.assets_dir, "snapshots", "events", f"{card_id}.jpg")
                snapshot.save(snapshot_path)

            else:
                logging.error(f'"info": "Failed to load image: {image_path}"')

        logging.info(f'"info": "Loaded {len(features)} images from {event_cards_dir}"')
        self.data["events"] = events

        if cfg.DEBUG_SAVE:
            card_id = 211
            image_file = f'event_{card_id}_{events[card_id]["zh-HANS"]}.png'
            image = Image.open(os.path.join(event_cards_dir, image_file))
            image = Image.alpha_composite(image, border)
            image.save(os.path.join(cfg.debug_dir, image_file))
            logging.debug(f'"info": "save {image_file} at {cfg.debug_dir}"')

        ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        for i in range(len(features)):
            ann.add_item(i, features[i])
        ann.build(cfg.ann_n_trees)
        ann.save(os.path.join(cfg.database_dir, cfg.events_ann_filename))
        self.events_ann = ann

        if cfg.DEBUG_SAVE:
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
                logging.debug(f'"info": {dt=}')
                logging.debug(f'"info": {my_ids=}')
                logging.debug(f'"info": {my_dists=}')
                found_name = events[my_ids[0]]['zh-HANS'] if my_dists[0] <= cfg.threshold else "None"
                logging.debug(f'"info": "{file}: {found_name}"')


    def _Update(self):
        self._UpdateControls()
        self._UpdateEventCards()

        with open(os.path.join(cfg.database_dir, cfg.db_filename), 'w', encoding='utf-8') as f:
            json.dump(self.data, f, indent=2, ensure_ascii=False)

    def Load(self):
        self.events_ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        self.events_ann.load(os.path.join(cfg.database_dir, cfg.events_ann_filename))
        self.rounds_ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        self.rounds_ann.load(os.path.join(cfg.database_dir, cfg.rounds_ann_filename))

        with open(os.path.join(cfg.database_dir, cfg.db_filename), encoding='utf-8') as f:
            self.data = json.load(f)
        
    def SearchByFeature(self, feature, ann_name):
        if ann_name == "event":
            ann = self.events_ann
        elif ann_name == "round":
            ann = self.rounds_ann
    
        ids, dists = ann.get_nns_by_vector(feature, n=1, include_distances=True)
        if ann_name == "round":
            ids, dists = ann.get_nns_by_vector(feature, n=5, include_distances=True)
            logging.debug(f'"info": {ids=}, {dists=}')
        return ids[0], dists[0]


if __name__ == '__main__':
    db = Database()
    db._Update()