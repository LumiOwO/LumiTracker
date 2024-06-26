from annoy import AnnoyIndex
import numpy as np
import cv2

import logging
import time
import json
import csv
import os
from pathlib import Path

from .config import cfg
from .enums import ECtrlType, EAnnType

def LoadImage(path):
    return cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_UNCHANGED)

def SaveImage(image, path):
    if image.dtype == np.float32:
        image *= 255
    cv2.imencode(Path(path).suffix, image)[1].tofile(path)

class ImageHash:
    """
    Hash encapsulation. Can be used for dictionary keys and comparisons.
    Reference: https://github.com/JohannesBuchner/imagehash/blob/master/imagehash/__init__.py
    """

    def _binary_array_to_hex(arr):
        """
        internal function to make a hex string out of a binary array.
        """
        bit_string = ''.join(str(b) for b in 1 * arr.flatten())
        width = int(np.ceil(len(bit_string) / 4))
        return '{:0>{width}x}'.format(int(bit_string, 2), width=width)

    def __init__(self, binary_array):
        self.hash = binary_array

    def __str__(self):
        return ImageHash._binary_array_to_hex(self.hash.flatten())

    def __repr__(self):
        return repr(self.hash)

    def __sub__(self, other):
        # type: (ImageHash) -> int
        if other is None:
            raise TypeError('Other hash must not be None.')
        if self.hash.size != other.hash.size:
            raise TypeError('ImageHashes must be of the same shape.', self.hash.shape, other.hash.shape)
        return np.count_nonzero(self.hash.flatten() != other.hash.flatten())

    def __eq__(self, other):
        # type: (object) -> bool
        if other is None:
            return False
        return np.array_equal(self.hash.flatten(), other.hash.flatten())  # type: ignore

    def __ne__(self, other):
        # type: (object) -> bool
        if other is None:
            return False
        return not np.array_equal(self.hash.flatten(), other.hash.flatten())  # type: ignore

    def __hash__(self):
        # this returns a 8 bit integer, intentionally shortening the information
        return sum([2**(i % 8) for i, v in enumerate(self.hash.flatten()) if v])

    def __len__(self):
        # Returns the bit length of the hash
        return self.hash.size

def PHash(gray_image, hash_size=8):
    """
    Perceptual Hash computation.

    Implementation follows http://www.hackerfactor.com/blog/index.php?/archives/432-Looks-Like-It.html

    Reference: https://github.com/JohannesBuchner/imagehash/blob/master/imagehash/__init__.py

    image: gray image, dtype == float32
    """
    # highfreq_factor = 4
    # img_size = hash_size * highfreq_factor
    # gray_image = cv2.resize(gray_image, (img_size, img_size), interpolation=cv2.INTER_AREA)
    
    dct = cv2.dct(gray_image)

    dctlowfreq = dct[:hash_size, :hash_size]
    med = np.median(dctlowfreq)
    diff = dctlowfreq > med
    
    return ImageHash(diff)


def DHash(gray_image, hash_size=8):
    """
    Difference Hash computation.

    following http://www.hackerfactor.com/blog/index.php?/archives/529-Kind-of-Like-That.html

    Reference: https://github.com/JohannesBuchner/imagehash/blob/master/imagehash/__init__.py

    computes differences horizontally

    """
    gray_image = cv2.resize(gray_image, (hash_size + 1, hash_size), interpolation=cv2.INTER_AREA)
    # compute differences between columns
    diff = gray_image[:, 1:] > gray_image[:, :-1]

    return ImageHash(diff)


def DHashVertical(gray_image, hash_size=8):
    """
    Difference Hash computation.

    following http://www.hackerfactor.com/blog/index.php?/archives/529-Kind-of-Like-That.html

    Reference: https://github.com/JohannesBuchner/imagehash/blob/master/imagehash/__init__.py

    computes differences vertically

    """
    gray_image = cv2.resize(gray_image, (hash_size, hash_size + 1), interpolation=cv2.INTER_AREA)
    # compute differences between rows
    diff = gray_image[1:, :] > gray_image[:-1, :]

    return ImageHash(diff)


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


CLAHE = cv2.createCLAHE(clipLimit=4.0, tileGridSize=(4, 4))

def Preprocess(image):
    # to gray image
    gray_image = cv2.cvtColor(image, cv2.COLOR_BGRA2GRAY)

    # remove high frequency noise
    gray_image = cv2.GaussianBlur(gray_image, (9, 9), 0)

    # histogram equalization
    gray_image = CLAHE.apply(gray_image)

    # to float buffer
    gray_image = gray_image.astype(np.float32) / 255.0

    return gray_image


def ExtractFeature(image):
    gray_image = Preprocess(image)

    feature = PHash(gray_image, hash_size=cfg.hash_size)
    feature = feature.hash.flatten()

    return feature

def FeatureDistance(feature1, feature2):
    return ImageHash(feature1) - ImageHash(feature2)

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
        self.anns       = [None] * EAnnType.ANN_COUNT.value
        self.rounds_ann = None

        Path(cfg.database_dir).mkdir(parents=True, exist_ok=True)
        if cfg.DEBUG:
            Path(cfg.debug_dir).mkdir(parents=True, exist_ok=True)

    def __getitem__(self, key):
        return self.data[key]

    def __setitem__(self, key, value):
        self.data[key] = value
    
    def _UpdateControls(self):
        n_controls = ECtrlType.CTRL_FEATURES_COUNT.value
        features = [None] * n_controls

        # Game start
        ctrl_id = ECtrlType.GAME_START.value
        image = LoadImage(os.path.join(cfg.cards_dir, "controls", f"control_{ctrl_id}.png"))
        feature = ExtractFeature(image)
        features[ctrl_id] = feature

        # Game over
        ctrl_id_first = ECtrlType.GAME_OVER_FIRST.value
        ctrl_id_last  = ECtrlType.GAME_OVER_LAST.value
        from .tasks import GameOverTask
        for i in range(ctrl_id_first, ctrl_id_last + 1):
            image = LoadImage(os.path.join(cfg.cards_dir, "controls", f"control_{i}.png"))
            main_content, valid = GameOverTask.CropMainContent(image)
            feature = ExtractFeature(main_content)
            features[i] = feature
            if cfg.DEBUG_SAVE:
                SaveImage(main_content, os.path.join(cfg.debug_dir, f"{ECtrlType(i).name.lower()}.png"))

        # Round
        ctrl_id_first = ECtrlType.ROUND_FIRST.value
        ctrl_id_last  = ECtrlType.ROUND_LAST.value
        from .tasks import RoundTask
        for i in range(ctrl_id_first, ctrl_id_last + 1):
            image = LoadImage(os.path.join(cfg.cards_dir, "controls", f"control_{i}.png"))
            main_content, valid = RoundTask.CropMainContent(image)
            feature = ExtractFeature(main_content)
            features[i] = feature
            if cfg.DEBUG_SAVE:
                SaveImage(main_content, os.path.join(cfg.debug_dir, f"{ECtrlType(i).name.lower()}.png"))

        ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        for i in range(len(features)):
            ann.add_item(i, features[i])
        ann.build(cfg.ann_n_trees)
        ann_filename = f"{EAnnType.CTRLS.name.lower()}.ann"
        ann.save(os.path.join(cfg.database_dir, ann_filename))
        self.anns[EAnnType.CTRLS.value] = ann

        if cfg.DEBUG:
            game_start_test_path = "temp/test/game_start_frame.png"
            image = LoadImage(game_start_test_path)
            feature = ExtractFeature(image)
            ctrl_id, dist = self.SearchByFeature(feature, EAnnType.CTRLS)
            print(f'"info": "{game_start_test_path}: {dist=}, {ECtrlType(ctrl_id).name}"')

            # Game over test
            image = LoadImage(os.path.join(cfg.debug_dir, f"GameOverTest.png"))
            main_content, valid = GameOverTask.CropMainContent(image)
            feature = ExtractFeature(main_content)
            ctrl_id, dist = self.SearchByFeature(feature, EAnnType.CTRLS)
            logging.debug(f'"info": "GameOverTest, {dist=}, {ECtrlType(ctrl_id).name}"')
            SaveImage(main_content, os.path.join(cfg.debug_dir, f"GameOverTest_MainContent.png"))

            # Rounds test
            n_rounds = 14
            for i in range(n_rounds):
                image = LoadImage(os.path.join(cfg.debug_dir, f"crop{i + 1}.png"))
                main_content, valid = RoundTask.CropMainContent(image)
                feature = ExtractFeature(main_content)
                ctrl_id, dist = self.SearchByFeature(feature, EAnnType.CTRLS)
                logging.debug(f'"info": "round{i + 1}, {dist=}"')


    def _UpdateEventCards(self):
        with open(os.path.join(cfg.cards_dir, "events.csv"), 
                    mode='r', newline='', encoding='utf-8') as csv_file:
            csv_reader = csv.DictReader(csv_file)
            csv_data = [row for row in csv_reader]

        # left   = 70
        # width  = 100
        # top    = 320
        # height = 300
        # crop_box0 = CropBox(left, top, left + width, top + height)
        # cfg.event_crop_box0 = ((crop_box0.left / 420, crop_box0.top / 720, crop_box0.width / 420, crop_box0.height / 720))
        # left   = 180
        # width  = 100
        # top    = 220
        # height = 200
        # crop_box1 = CropBox(left, top, left + width, top + height)
        # cfg.event_crop_box1 = ((crop_box1.left / 420, crop_box1.top / 720, crop_box1.width / 420, crop_box1.height / 720))
        # left   = 250
        # width  = 100
        # top    = 450
        # height = 100
        # crop_box2 = CropBox(left, top, left + width, top + height)
        # cfg.event_crop_box2 = ((crop_box2.left / 420, crop_box2.top / 720, crop_box2.width / 420, crop_box2.height / 720))

        # do not call Tick() when updating database 
        from .tasks import CardPlayedTask
        task = CardPlayedTask(None, None)
        task._ResizeFeatureBuffer(420, 720)
        task.crop_box = CropBox(0, 0, 420, 720)

        event_cards_dir = os.path.join(cfg.cards_dir, "events")
        n_images = len(csv_data)
        events   = [None] * n_images
        features = [None] * n_images
        for row in csv_data:
            card_id = int(row["id"])
            image_file = f'event_{card_id}_{row["zh-HANS"]}.png'

            image_path = os.path.join(event_cards_dir, image_file)
            image = LoadImage(image_path)
            if image is None:
                logging.error(f'"info": "Failed to load image: {image_path}"')
                exit(1)
        
            # create snapshot
            top    = int(row["snapshot_top"])
            left   = 12
            height = 150
            crop_box = CropBox(left, top, 420 - left, top + height)
            snapshot = image[
                crop_box.top  : crop_box.bottom, 
                crop_box.left : crop_box.right
            ]
            snapshot_path = os.path.join(
                cfg.assets_dir, "snapshots", "events", f"{card_id}.jpg")
            SaveImage(snapshot, snapshot_path)

            task.frame_buffer = image
            task._UpdateFeatureBuffer()

            feature = ExtractFeature(task.feature_buffer)
            # gray_image = Preprocess(task.feature_buffer)
            # SaveImage(gray_image, snapshot_path)

            features[card_id] = feature
            events[card_id]   = row

        if cfg.DEBUG:
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
            image = LoadImage(os.path.join(event_cards_dir, image_file))
            SaveImage(image, os.path.join(cfg.debug_dir, image_file))
            logging.debug(f'"info": "save {image_file} at {cfg.debug_dir}"')

        # cfg.ann_index_len = len(features[0])
        ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        for i in range(len(features)):
            ann.add_item(i, features[i])
        ann.build(cfg.ann_n_trees)
        ann_filename = f"{EAnnType.EVENTS.name.lower()}.ann"
        ann.save(os.path.join(cfg.database_dir, ann_filename))
        self.anns[EAnnType.EVENTS.value] = ann

        if cfg.DEBUG_SAVE:
            test_dir = os.path.join(cfg.debug_dir, "test")
            files = os.listdir(test_dir)
            image_files = [file for file in files if (file.lower().endswith(".png") or file.lower().endswith(".jpg"))]
            image_files = sorted(image_files)
            for file in image_files:
                image = LoadImage(os.path.join(test_dir, file))
                begin_time = time.perf_counter()
                # print(image.shape, image.dtype)
                if len(image.shape) == 2:
                    image = cv2.cvtColor(image, cv2.COLOR_GRAY2BGRA)
                my_feature = ExtractFeature(image)
                my_ids, my_dists = ann.get_nns_by_vector(my_feature, n=20, include_distances=True)

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
        n_anns = EAnnType.ANN_COUNT.value
        for i in range(n_anns):
            ann = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
            ann_filename = f"{EAnnType(i).name.lower()}.ann"
            ann.load(os.path.join(cfg.database_dir, ann_filename))
            self.anns[i] = ann

        with open(os.path.join(cfg.database_dir, cfg.db_filename), encoding='utf-8') as f:
            self.data = json.load(f)
        
    def SearchByFeature(self, feature, ann_type):
        ann = self.anns[ann_type.value]

        # !!!!! Must use a large n, or it may not find the optimal result !!!!!
        ids, dists = ann.get_nns_by_vector(feature, n=20, include_distances=True)
        return ids[0], dists[0]


if __name__ == '__main__':
    db = Database()
    db._Update()