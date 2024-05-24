from annoy import AnnoyIndex
from PIL import Image
import imagehash

import time
import json
import os
from pathlib import Path

class cfg:
    DEBUG               = True
    debug_dir           = "temp"

    # file paths
    database_dir        = "assets/database"
    events_ann_filename = "events.ann"
    db_filename         = "db.json"

    cards_dir           = "cards"
    border_filename     = "tcg_border_bg.png"

    # feature extraction
    hash_size           = 8
    threshold           = 10
    ann_metric          = "hamming"   # ["angular", "euclidean", "manhattan", "hamming", "dot"]
    ann_n_trees         = 10

    # card info
    event_card_size     = (420, 720)

# Only accept PIL Image with RGB format
def ExtractFeature(image: Image):
    feature = imagehash.dhash(image, hash_size=cfg.hash_size)
    feature = feature.hash.flatten()
    # print(feature)

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

    def UpdateEventCards(self):
        event_cards_dir = os.path.join(cfg.cards_dir, "events")
        files = os.listdir(event_cards_dir)
        image_files = [file for file in files if file.lower().endswith(".png")]

        border = Image.open(os.path.join(cfg.cards_dir, cfg.border_filename)).convert("RGBA")

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
                print(f"Failed to load image: {image_path}")

        print(f"Loaded {len(features)} images from {event_cards_dir}")
        self.data["events"] = events

        if cfg.DEBUG:
            image.save(os.path.join(cfg.debug_dir, image_file))
            print(f"save {image_file} at {cfg.debug_dir}")

        ann = AnnoyIndex(cfg.hash_size * cfg.hash_size, cfg.ann_metric)
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

                my_feature = ExtractFeature(image.convert("RGB"))
                my_ids, my_dists = ann.get_nns_by_vector(my_feature, n=10, include_distances=True)
                print(my_ids)
                print(my_dists)
                found_name = events[my_ids[0]]['name_CN'] if my_dists[0] <= cfg.threshold else "None"
                print(f"{file}: {found_name}")

            # save last one
            for i, card_id in enumerate(my_ids):
                image = Image.open(os.path.join(event_cards_dir, events[card_id]["filename"]))
                image.save(f"temp/found{i}.png")


    def Update(self):
        self.UpdateEventCards()

        with open(os.path.join(cfg.database_dir, cfg.db_filename), 'w', encoding='utf-8') as f:
            json.dump(self.data, f, indent=2, ensure_ascii=False)
    
    def Load(self):
        self.events_ann = AnnoyIndex(cfg.hash_size * cfg.hash_size, cfg.ann_metric)
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