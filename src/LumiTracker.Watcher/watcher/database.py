from annoy import AnnoyIndex
from PIL import Image, ImageOps, ImageEnhance
import numpy as np
import imagehash

import logging
import time
import json
import csv
import os
from pathlib import Path

from .config import cfg

class CropBox:
    def __init__(self, left, top, right, bottom):
        self.left   = left
        self.top    = top
        self.right  = right
        self.bottom = bottom
    
    @property
    def width(self):
        return self.right - self.left

    @property
    def height(self):
        return self.bottom - self.top
    
    def __str__(self):
        return f"CropBox(left={self.left}, top={self.top}, right={self.right}, bottom={self.bottom})"


def ExtractFeature(image: Image):
    # histogram equalization
    image = image.convert('L')
    image = ImageOps.equalize(image)

    # feature1 = imagehash.colorhash(image, binbits=6)
    # feature1 = feature1.hash.flatten()

    # feature2 = imagehash.average_hash(image, hash_size=cfg.hash_size)
    # feature2 = feature2.hash.flatten()

    # feature = np.append(feature1, feature2)

    # perceptual hash
    feature = imagehash.phash(image, hash_size=cfg.hash_size, highfreq_factor=1)
    feature = feature.hash.flatten()

    return feature

'''
    image_buffer: (h, w, 4), BGRX
'''
def ExtractFeatureFromBuffer(image_buffer: np.ndarray):
    image = Image.frombuffer(
        'RGBX', 
        (image_buffer.shape[1], image_buffer.shape[0]), 
        image_buffer, 
        'raw', 
        'BGRX', 
        0, 
        1
        )
    return ExtractFeature(image)

def FeatureDistance(feature1, feature2):
    return imagehash.ImageHash(feature1) - imagehash.ImageHash(feature2)

def HashToFeature(hash_str):
    binary_string = bin(int(hash_str, 16))[2:]
    expected_length = len(hash_str) * 4
    binary_string = binary_string.zfill(expected_length)
    bool_list = [bit == '1' for bit in binary_string]
    feature = np.array(bool_list, dtype=bool)
    return feature

def CopyEventFeatureRegion(dst_buffer, src_buffer, crop_boxes):
    dst_buffer[:crop_boxes[0].height, :crop_boxes[0].width] = src_buffer[
        crop_boxes[0].top  : crop_boxes[0].bottom, 
        crop_boxes[0].left : crop_boxes[0].right
    ]

    dst_buffer[:crop_boxes[1].height, crop_boxes[0].width:] = src_buffer[
        crop_boxes[1].top  : crop_boxes[1].bottom, 
        crop_boxes[1].left : crop_boxes[1].right
    ]

    dst_buffer[crop_boxes[1].height:, crop_boxes[0].width:] = src_buffer[
        crop_boxes[2].top  : crop_boxes[2].bottom, 
        crop_boxes[2].left : crop_boxes[2].right
    ]

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
        round_feature = ExtractFeature(game_round.convert("RGB"))
        controls["GameRound"] = str(imagehash.ImageHash(round_feature))
        if cfg.DEBUG:
            n_rounds = 14
            for i in range(n_rounds):
                round_image = Image.open(os.path.join(cfg.debug_dir, f"crop{i + 1}.png")).convert("RGBA")
                feature = ExtractFeature(round_image.convert("RGB"))
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

        left   = 70
        width  = 100
        top    = 320
        height = 300
        crop_box0 = CropBox(left, top, left + width, top + height)
        cfg.event_crop_box0 = ((crop_box0.left / 420, crop_box0.top / 720, crop_box0.right / 420, crop_box0.bottom / 720))
        left   = 180
        width  = 100
        top    = 220
        height = 200
        crop_box1 = CropBox(left, top, left + width, top + height)
        cfg.event_crop_box1 = ((crop_box1.left / 420, crop_box1.top / 720, crop_box1.right / 420, crop_box1.bottom / 720))
        left   = 250
        width  = 100
        top    = 450
        height = 100
        crop_box2 = CropBox(left, top, left + width, top + height)
        cfg.event_crop_box2 = ((crop_box2.left / 420, crop_box2.top / 720, crop_box2.right / 420, crop_box2.bottom / 720))

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
                # create snapshot
                top    = int(row["snapshot_top"])
                left   = 12
                height = 150
                crop_box = (left, top, 420 - left, top + height)
                snapshot = image.crop(crop_box).convert("RGB")
                snapshot_path = os.path.join(
                    cfg.assets_dir, "snapshots", "events", f"{card_id}.jpg")
                snapshot.save(snapshot_path)

                # create subimage for feature
                image_array = np.asarray(image)
                subimage1 = image_array[crop_box0.top:crop_box0.bottom, crop_box0.left:crop_box0.right]
                subimage2 = image_array[crop_box1.top:crop_box1.bottom, crop_box1.left:crop_box1.right]
                subimage3 = image_array[crop_box2.top:crop_box2.bottom, crop_box2.left:crop_box2.right]
                concatenated_array = np.hstack((subimage1, np.vstack((subimage2, subimage3))))

                feature_image = Image.fromarray(concatenated_array)
                feature = ExtractFeature(feature_image)
                # feature_image.convert("RGB").save(snapshot_path)

                features[card_id] = feature
                events[card_id]   = row

            else:
                logging.error(f'"info": "Failed to load image: {image_path}"')
        
        if cfg.DEBUG:
            logging.debug(f"{FeatureDistance(features[46], features[204])}")
            logging.debug(f"{FeatureDistance(features[310], features[287])}")
            n = len(features)
            min_dist = 100000
            from collections import defaultdict
            close_dists = defaultdict(list)
            for i in range(n):
                for j in range(i + 1, n):
                    dist = FeatureDistance(features[i], features[j])
                    min_dist = min(dist, min_dist)
                    if dist <= cfg.threshold:
                        close_dists[dist].append(f'{i}{events[i]["zh-HANS"]} <-----> {j}{events[j]["zh-HANS"]}') 
            
            close_dists = {key: close_dists[key] for key in sorted(close_dists)}
            logging.warning(f'{json.dumps(close_dists, indent=2, ensure_ascii=False)}')
            logging.warning(f'{min_dist=}')


        logging.info(f'"info": "Loaded {len(features)} images from {event_cards_dir}"')
        self.data["events"] = events

        if cfg.DEBUG_SAVE:
            card_id = 211
            image_file = f'event_{card_id}_{events[card_id]["zh-HANS"]}.png'
            image = Image.open(os.path.join(event_cards_dir, image_file))
            image = Image.alpha_composite(image, border)
            image.save(os.path.join(cfg.debug_dir, image_file))
            logging.debug(f'"info": "save {image_file} at {cfg.debug_dir}"')

        cfg.ann_index_len = len(features[0])
        ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        for i in range(len(features)):
            ann.add_item(i, features[i])
        ann.build(cfg.ann_n_trees)
        ann.save(os.path.join(cfg.database_dir, cfg.events_ann_filename))
        self.events_ann = ann

        if cfg.DEBUG_SAVE:
            test_dir = os.path.join(cfg.debug_dir, "test")
            files = os.listdir(test_dir)
            image_files = [file for file in files if (file.lower().endswith(".png") or file.lower().endswith(".jpg"))]
            image_files = sorted(image_files)
            for file in image_files:
                image = Image.open(os.path.join(test_dir, file)).convert("RGBA")
                begin_time = time.perf_counter()

                my_feature = ExtractFeature(image)
                my_ids, my_dists = ann.get_nns_by_vector(my_feature, n=10, include_distances=True)

                dt = time.perf_counter() - begin_time
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
        
        with open("assets/config.json", 'w') as f:
            json.dump(vars(cfg), f, indent=2, ensure_ascii=False)

    def Load(self):
        self.events_ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        self.events_ann.load(os.path.join(cfg.database_dir, cfg.events_ann_filename))

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