from annoy import AnnoyIndex
import numpy as np
import cv2

import logging
import time
import json
import csv
import os
import shutil
from pathlib import Path

from .config import cfg, LogDebug, LogInfo, LogWarning, LogError
from .enums import ECtrlType, EAnnType, EActionCardType, EElementType, ECostType

def LoadImage(path):
    return cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_UNCHANGED)

def SaveImage(image, path, remove_alpha=False):
    if remove_alpha and len(image.shape) == 3 and image.shape[-1] == 4:
        image = cv2.cvtColor(image, cv2.COLOR_BGRA2BGR)
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

def MultiPHash(gray_image, hash_size=8):
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

    # ahash
    dctlowfreq = dct[:hash_size, :hash_size]
    med = np.median(dctlowfreq)
    diff = dctlowfreq > med
    ahash = ImageHash(diff)

    # dhash vertical
    dctlowfreq = dct[:hash_size + 1, :hash_size]
    diff = dctlowfreq[1:, :] > dctlowfreq[:-1, :]
    dhash = ImageHash(diff)
    
    return ahash, dhash

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
    
    def __repr__(self):
        return f"CropBox(left={self.left}, top={self.top}, right={self.right}, bottom={self.bottom})"

    def Merge(self, other):
        self.left   = min(self.left  , other.left)
        self.top    = min(self.top   , other.top)
        self.right  = max(self.right , other.right)
        self.bottom = max(self.bottom, other.bottom)


CLAHE = cv2.createCLAHE(clipLimit=4.0, tileGridSize=(4, 4))

def Preprocess(image):
    # to gray image
    gray_image = cv2.cvtColor(image, cv2.COLOR_BGRA2GRAY)

    # remove high frequency noise
    # gray_image = cv2.GaussianBlur(gray_image, (9, 9), 0)

    # histogram equalization
    gray_image = CLAHE.apply(gray_image)

    # to float buffer
    gray_image = gray_image.astype(np.float32) / 255.0

    return gray_image


def ExtractFeature(image):
    gray_image = Preprocess(image)

    feature = DHash(gray_image, hash_size=cfg.hash_size)
    feature = feature.hash.flatten()

    return feature

def ExtractMultiFeatures(image):
    gray_image = Preprocess(image)

    ahash, dhash = MultiPHash(gray_image, hash_size=cfg.hash_size)
    ahash = ahash.hash.flatten()
    dhash = dhash.hash.flatten()

    return ahash, dhash

def FeatureDistance(feature1, feature2):
    return ImageHash(feature1) - ImageHash(feature2)

def HashToFeature(hash_str):
    binary_string = bin(int(hash_str, 16))[2:]
    expected_length = len(hash_str) * 4
    binary_string = binary_string.zfill(expected_length)
    bool_list = [bit == '1' for bit in binary_string]
    feature = np.array(bool_list, dtype=bool)
    return feature


def CardName(card_id, db, lang="zh-HANS"):
    return db["actions"][card_id][lang] if card_id >= 0 else "None"

class ActionCardHandler:
    def __init__(self):
        self.feature_buffer = None
        self.crop_cfgs      = (cfg.action_crop_box0, cfg.action_crop_box1, cfg.action_crop_box2)

        self.frame_buffer   = None
        self.crop_box       = None  # init when resize

    def ExtractCardFeatures(self):
        # Get action card region
        region_buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]

        # Crop action card and get feature buffer
        self.feature_buffer[:self.feature_crops[0].height, :self.feature_crops[0].width] = region_buffer[
            self.feature_crops[0].top  : self.feature_crops[0].bottom, 
            self.feature_crops[0].left : self.feature_crops[0].right
        ]

        self.feature_buffer[:self.feature_crops[1].height, self.feature_crops[0].width:] = region_buffer[
            self.feature_crops[1].top  : self.feature_crops[1].bottom, 
            self.feature_crops[1].left : self.feature_crops[1].right
        ]

        self.feature_buffer[self.feature_crops[1].height:, self.feature_crops[0].width:] = region_buffer[
            self.feature_crops[2].top  : self.feature_crops[2].bottom, 
            self.feature_crops[2].left : self.feature_crops[2].right
        ]

        # Extract feature
        features = ExtractMultiFeatures(self.feature_buffer)
        return features

    def Update(self, frame_buffer, db):
        self.frame_buffer = frame_buffer

        # TODO: fix the temp fix for the tokens of Counting Down to 3 
        # 319, 320, 321 -> 301
        def _tempfix(card_id):
            if card_id == 319 or card_id == 320 or card_id == 321:
                card_id = 301
            return card_id

        ahash, dhash = self.ExtractCardFeatures()
        card_ids_a, dists_a = db.SearchByFeature(ahash, EAnnType.ACTIONS_A)
        card_ids_d, dists_d = db.SearchByFeature(dhash, EAnnType.ACTIONS_D)

        card_id_a = _tempfix(card_ids_a[0])
        card_id_d = _tempfix(card_ids_d[0])
        threshold = cfg.threshold
        if dists_a[0] <= threshold and dists_d[0] <= threshold and card_id_a == card_id_d:
            card_id = card_id_a
        else:
            card_id = -1
        # if card_id >= 0:
        #     LogDebug(
        #         card_a=CardName(card_id_a, db),
        #         card_d=CardName(card_id_d, db),
        #         dists=(dists_a[:3], dists_d[:3]))

        dist = min(dists_a[0], dists_d[0])
        return card_id, dist, (dists_a[:3], dists_d[:3])
    
    def OnResize(self, crop_box):
        self.crop_box = crop_box

        self._ResizeFeatureCrops(crop_box.width, crop_box.height)

        feature_buffer_width  = self.feature_crops[0].width + self.feature_crops[1].width
        feature_buffer_height = self.feature_crops[0].height
        self.feature_buffer = np.zeros(
            (feature_buffer_height, feature_buffer_width, 4), dtype=np.uint8)

    def _ResizeFeatureCrops(self, width, height):
        # ////////////////////////////////
        # //    Feature buffer
        # //    Stacked by cropped region
        # //    
        # //    ---------------------
        # //    |         |         |
        # //    |         |         |
        # //    |    0    |    1    |
        # //    |         |         |
        # //    |         |         |
        # //    |         |---------|
        # //    |         |         |
        # //    |         |    2    |
        # //    |         |         |
        # //    |         |         |
        # //    |---------|---------|
        # //
        # ////////////////////////////////
        feature_crop_l0 = round(self.crop_cfgs[0][0] * width)
        feature_crop_t0 = round(self.crop_cfgs[0][1] * height)
        feature_crop_w0 = round(self.crop_cfgs[0][2] * width)
        feature_crop_h0 = round(self.crop_cfgs[0][3] * height)
        feature_crop0 = CropBox(
            feature_crop_l0,
            feature_crop_t0,
            feature_crop_l0 + feature_crop_w0,
            feature_crop_t0 + feature_crop_h0,
        )

        feature_crop_l1 = round(self.crop_cfgs[1][0] * width)
        feature_crop_t1 = round(self.crop_cfgs[1][1] * height)
        feature_crop_w1 = round(self.crop_cfgs[1][2] * width)
        feature_crop_h1 = round(self.crop_cfgs[1][3] * height)
        feature_crop1 = CropBox(
            feature_crop_l1,
            feature_crop_t1,
            feature_crop_l1 + feature_crop_w1,
            feature_crop_t1 + feature_crop_h1,
        )

        feature_crop_l2 = round(self.crop_cfgs[2][0] * width)
        feature_crop_t2 = round(self.crop_cfgs[2][1] * height)
        feature_crop_w2 = feature_crop_w1
        feature_crop_h2 = feature_crop_h0 - feature_crop_h1
        feature_crop2 = CropBox(
            feature_crop_l2,
            feature_crop_t2,
            feature_crop_l2 + feature_crop_w2,
            feature_crop_t2 + feature_crop_h2,
        )
        self.feature_crops = [feature_crop0, feature_crop1, feature_crop2]

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
            ctrl_ids, dists = self.SearchByFeature(feature, EAnnType.CTRLS)
            LogDebug(infp=f'{game_start_test_path}, {dists[0]=}, {ECtrlType(ctrl_ids[0]).name}')

            # Game over test
            image = LoadImage(os.path.join(cfg.debug_dir, f"GameOverTest.png"))
            main_content, valid = GameOverTask.CropMainContent(image)
            feature = ExtractFeature(main_content)
            ctrl_ids, dists = self.SearchByFeature(feature, EAnnType.CTRLS)
            LogDebug(info=f"GameOverTest, {dists[0]=}, {ECtrlType(ctrl_ids[0]).name}")
            SaveImage(main_content, os.path.join(cfg.debug_dir, f"GameOverTest_MainContent.png"))

            # Rounds test
            n_rounds = 14
            for i in range(n_rounds):
                image = LoadImage(os.path.join(cfg.debug_dir, f"crop{i + 1}.png"))
                main_content, valid = RoundTask.CropMainContent(image)
                feature = ExtractFeature(main_content)
                ctrl_ids, dists = self.SearchByFeature(feature, EAnnType.CTRLS)
                LogDebug(info=f"round{i + 1}, {dists[0]=}")


    def _UpdateActionCards(self, save_image_assets):
        with open(os.path.join(cfg.cards_dir, "actions.csv"), 
                    mode='r', newline='', encoding='utf-8') as csv_file:
            csv_reader = csv.DictReader(csv_file)
            csv_data = [row for row in csv_reader]
        num_actions = len(csv_data)
        
        with open(os.path.join(cfg.cards_dir, "tokens.csv"), 
                    mode='r', newline='', encoding='utf-8') as tokens_file:
            tokens_reader = csv.DictReader(tokens_file)
            for row in tokens_reader:
                row["id"] = int(row["id"]) + num_actions
                csv_data.append(row)

        # left   = 70
        # width  = 100
        # top    = 320
        # height = 300
        # crop_box0 = CropBox(left, top, left + width, top + height)
        # cfg.action_crop_box0 = ((crop_box0.left / 420, crop_box0.top / 720, crop_box0.width / 420, crop_box0.height / 720))
        # left   = 180
        # width  = 100
        # top    = 220
        # height = 200
        # crop_box1 = CropBox(left, top, left + width, top + height)
        # cfg.action_crop_box1 = ((crop_box1.left / 420, crop_box1.top / 720, crop_box1.width / 420, crop_box1.height / 720))
        # left   = 230
        # width  = 100
        # top    = 505
        # height = 100
        # crop_box2 = CropBox(left, top, left + width, top + height)
        # cfg.action_crop_box2 = ((crop_box2.left / 420, crop_box2.top / 720, crop_box2.width / 420, crop_box2.height / 720))

        handler = ActionCardHandler()
        handler.OnResize(CropBox(0, 0, 420, 720))

        action_cards_dir = os.path.join(cfg.cards_dir, "actions")
        n_images = len(csv_data)
        actions  = [None] * n_images
        ahashs   = [None] * n_images
        dhashs   = [None] * n_images
        for image_idx, row in enumerate(csv_data):
            card_id = int(row["id"])
            if image_idx < num_actions:
                image_file = f'action_{card_id}_{row["zh-HANS"]}.png'
            else:
                image_file = f'tokens/token_{card_id - num_actions}_{row["zh-HANS"]}.png'

            image_path = os.path.join(action_cards_dir, image_file)
            image = LoadImage(image_path)
            if image is None:
                LogError(info=f"Failed to load image: {image_path}")
                exit(1)
        
            snapshot_path = os.path.join(
                cfg.assets_dir, "images", "snapshots", f"{card_id}.jpg")
            if save_image_assets:
                # create snapshot
                top    = int(row["snapshot_top"])
                left   = 12
                height = 150
                crop_box = CropBox(left, top, 420 - left, top + height)
                snapshot = image[
                    crop_box.top  : crop_box.bottom, 
                    crop_box.left : crop_box.right
                ]
                SaveImage(snapshot, snapshot_path)

            handler.frame_buffer = image
            ahash, dhash = handler.ExtractCardFeatures()
            # SaveImage(handler.feature_buffer, snapshot_path)

            cost_type = row["element"]
            # special case: talent for Attack
            if "," in row["cost"]:
                cost_type += "Attack"
                cost = sum([int(val) for val in row["cost"].split(",")])
            else:
                cost = int(row["cost"])
            cost_type = ECostType[cost_type].value

            action = {
                "zh-HANS" : row["zh-HANS"],
                "en-US"   : row["en-US"],
                "type"    : EActionCardType[row["type"]].value,
                "cost"    : (cost, cost_type),
            }

            ahashs[card_id]   = ahash
            dhashs[card_id]   = dhash
            actions[card_id]  = action

        if cfg.DEBUG:
            n = len(ahashs)
            from collections import defaultdict

            # ahash
            min_dist = 100000
            close_dists = defaultdict(list)
            for i in range(n):
                for j in range(i + 1, n):
                    dist = FeatureDistance(ahashs[i], ahashs[j])
                    min_dist = min(dist, min_dist)
                    if dist <= cfg.threshold:
                        close_dists[dist].append(f'{i}{actions[i]["zh-HANS"]} <-----> {j}{actions[j]["zh-HANS"]}') 
            
            close_dists = {key: close_dists[key] for key in sorted(close_dists)}
            LogWarning(
                indent=2,
                min_dist=min_dist,
                close_dists=close_dists, 
                )

            # dhash
            min_dist = 100000
            close_dists = defaultdict(list)
            for i in range(n):
                for j in range(i + 1, n):
                    dist = FeatureDistance(dhashs[i], dhashs[j])
                    min_dist = min(dist, min_dist)
                    if dist <= cfg.threshold:
                        close_dists[dist].append(f'{i}{actions[i]["zh-HANS"]} <-----> {j}{actions[j]["zh-HANS"]}') 
            
            close_dists = {key: close_dists[key] for key in sorted(close_dists)}
            LogWarning(
                indent=2,
                min_dist=min_dist,
                close_dists=close_dists, 
                )

        LogInfo(info=f"Loaded {len(ahashs)} images from {action_cards_dir}")
        self.data["actions"] = actions

        if cfg.DEBUG_SAVE:
            card_id = 211
            image_file = f'action_{card_id}_{actions[card_id]["zh-HANS"]}.png'
            image = LoadImage(os.path.join(action_cards_dir, image_file))
            SaveImage(image, os.path.join(cfg.debug_dir, image_file))
            LogDebug(info=f"save {image_file} at {cfg.debug_dir}")

        # cfg.ann_index_len = len(features[0])
        ann_ahash = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        for i in range(len(ahashs)):
            ann_ahash.add_item(i, ahashs[i])
        ann_ahash.build(cfg.ann_n_trees)
        ann_filename = f"{EAnnType.ACTIONS_A.name.lower()}.ann"
        ann_ahash.save(os.path.join(cfg.database_dir, ann_filename))
        self.anns[EAnnType.ACTIONS_A.value] = ann_ahash

        ann_dhash = AnnoyIndex(cfg.ann_index_len, cfg.ann_metric)
        for i in range(len(dhashs)):
            ann_dhash.add_item(i, dhashs[i])
        ann_dhash.build(cfg.ann_n_trees)
        ann_filename = f"{EAnnType.ACTIONS_D.name.lower()}.ann"
        ann_dhash.save(os.path.join(cfg.database_dir, ann_filename))
        self.anns[EAnnType.ACTIONS_D.value] = ann_dhash

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
                if len(image.shape) == 3 and image.shape[-1] == 3:
                    image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)
                height, width = image.shape[:2]
                
                handler = ActionCardHandler()
                handler.OnResize(CropBox(0, 0, width, height))
                handler.frame_buffer = image
                ahash, dhash = handler.ExtractCardFeatures()
                card_ids_a, dists_a = db.SearchByFeature(ahash, EAnnType.ACTIONS_A)
                card_ids_d, dists_d = db.SearchByFeature(dhash, EAnnType.ACTIONS_D)

                dt = time.perf_counter() - begin_time
                name_a = actions[card_ids_a[0]]['zh-HANS']
                name_d = actions[card_ids_d[0]]['zh-HANS']
                LogDebug(
                    indent=2, file=file,
                    name_a=name_a, card_ids_a=card_ids_a[:3], dists_a=dists_a[:3],
                    name_d=name_d, card_ids_d=card_ids_d[:3], dists_d=dists_d[:3],
                    dt=dt, 
                    )

    def _UpdateCharacters(self, save_image_assets):
        with open(os.path.join(cfg.cards_dir, "characters.csv"), 
                    mode='r', newline='', encoding='utf-8') as csv_file:
            reader = csv.DictReader(csv_file)
            data = [row for row in reader]
        num_characters = len(data)

        talent_to_character = {}
        characters = [None] * num_characters
        for i, row in enumerate(data):
            character = {
                "zh-HANS"    : row["zh-HANS"],
                "element"    : EElementType[row["element"]].value,
                "is_monster" : True if row["is_monster"] == "1" else False,
            }
            characters[i] = character
            talent_id = int(row["talent_id"])
            talent_to_character[talent_id] = int(row["id"])

            if save_image_assets:
                src_file = os.path.join(
                    cfg.cards_dir, "avatars", f'avatar_{row["id"]}_{row["zh-HANS"]}.png'
                    )
                dst_file = os.path.join(
                    cfg.assets_dir, "images", "avatars", f'{row["id"]}.png'
                    )
                shutil.copy(src_file, dst_file)

        self.data["characters"] = characters
        self.data["talent_to_character"] = talent_to_character

    def _UpdateExtraInfos(self):
        # share code
        with open(os.path.join(cfg.cards_dir, "share_code.csv"), 
                    mode='r', newline='', encoding='utf-8') as share_code_file:
            share_code_reader = csv.DictReader(share_code_file)
            share_code_data = [row for row in share_code_reader]
        share_to_internal = [0] + [None] * len(share_code_data)
        for row in share_code_data:
            share_id = int(row["share_id"])
            internal_id = int(row["internal_id"])
            is_character = (int(row["is_character"]) == 1)
            if is_character:
                share_to_internal[share_id] = -(internal_id + 1)
            else:
                share_to_internal[share_id] = internal_id + 1

        self.data["share_to_internal"] = share_to_internal

        # artifacts
        with open(os.path.join(cfg.cards_dir, "artifacts.csv"), 
                    mode='r', newline='', encoding='utf-8') as artifacts_file:
            artifacts_reader = csv.DictReader(artifacts_file)
            artifacts_data = [row for row in artifacts_reader]
        artifacts_order = {}
        for i, row in enumerate(artifacts_data):
            internal_id = int(row["internal_id"])
            artifacts_order[internal_id] = i

        self.data["artifacts_order"] = artifacts_order

        # cost images
        if save_image_assets:
            for name in ECostType.__members__:
                src_file = os.path.join(
                    cfg.cards_dir, "costs", f'{name}.png'
                    )
                dst_file = os.path.join(
                    cfg.assets_dir, "images", "costs", f'{name}.png'
                    )
                shutil.copy(src_file, dst_file)

    def _Update(self, save_image_assets):
        self._UpdateControls()
        self._UpdateActionCards(save_image_assets)
        self._UpdateCharacters(save_image_assets)
        self._UpdateExtraInfos()

        with open(os.path.join(cfg.database_dir, cfg.db_filename), 'w', encoding='utf-8') as f:
            json.dump(self.data, f, indent=None, ensure_ascii=False)
        
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
        return ids, dists


if __name__ == '__main__':
    import sys
    save_image_assets = (len(sys.argv) > 1 and sys.argv[1] == "image")

    db = Database()
    db._Update(save_image_assets)