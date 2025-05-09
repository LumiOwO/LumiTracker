from annoy import AnnoyIndex
import numpy as np
import cv2

import logging
import time
import json
import csv
import os
import re
import shutil
from pathlib import Path
from collections import defaultdict

from .config import cfg, LogDebug, LogInfo, LogWarning, LogError
from .enums import ECtrlType, EAnnType, EActionCardType, EElementType, ECostType, ELanguage
from .feature import CropBox, ActionCardHandler, CharacterCardHandler, FeatureDistance, GetHashSize, ImageHash
from .feature import ExtractFeature_Control, ExtractFeature_Digit, ExtractFeature_Control_Single

def Alert(info):
    print("***** Fatal Error: " + info)
    exit(1)

def LoadImage(path):
    return cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_UNCHANGED)

def SaveImage(image, path, remove_alpha=False):
    if remove_alpha and len(image.shape) == 3 and image.shape[-1] == 4:
        image = cv2.cvtColor(image, cv2.COLOR_BGRA2BGR)
    if image.dtype == np.float32:
        image *= 255
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    cv2.imencode(Path(path).suffix, image)[1].tofile(path)

def CheckHashDistances(test_name, hashs, name_func):
    n = len(hashs)
    min_dist = 100000
    close_dists = defaultdict(list)
    for i in range(n):
        for j in range(i + 1, n):
            dist = FeatureDistance(hashs[i], hashs[j])
            min_dist = min(dist, min_dist)
            if dist <= cfg.threshold:
                close_dists[dist].append(f'{i}{name_func(i)} <-----> {j}{name_func(j)}') 
    
    close_dists = {key: close_dists[key] for key in sorted(close_dists)}
    LogWarning(
        indent=2,
        test_name=test_name,
        min_dist=min_dist,
        close_dists=close_dists, 
        )

def StringToVariableName(s):
    # Remove apostrophes
    s = s.replace("'", "")
    # Remove all non-alphanumeric characters except for spaces
    s = re.sub(r"[^a-zA-Z0-9\s]", " ", s)
    # Split the string by spaces
    words = s.split()
    # Capitalize the first letter of each word
    words = [word.capitalize() for word in words]
    # Join the words together to form the variable name
    return "".join(words)

class DatabaseUpdateContext:
    def __init__(self):
        self.save_image_assets      = False
        self.num_sharable_actions   = 0
        self.num_extra_goldens      = 0
        self.num_arcane_legends     = 0
        self.num_artifacts          = 0
        self.num_characters         = 0

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
    
    def _UpdateControls(self, ctx: DatabaseUpdateContext):
        LogDebug(ctrl_types=[(e.name, e.value) for e in ECtrlType], indent=2)

        n_controls = ECtrlType.NUM_CTRLS_ANN.value
        features = [None] * n_controls

        controls_dir = os.path.join(cfg.cards_dir, "controls")
        for i in range(n_controls):
            name = ECtrlType(i).name.lower()
            image = LoadImage(os.path.join(controls_dir, f"control_{name}.png"))
            feature = ExtractFeature_Control(image)
            features[i] = feature

        ann = self.CreateAndSaveAnn(features, EAnnType.CTRLS)
        self.anns[EAnnType.CTRLS.value] = ann

        print(f"Loaded {n_controls} images from {controls_dir}")

        single_ctrls = []    
        for i in range(ECtrlType.CTRL_SINGLE_FIRST.value, ECtrlType.CTRL_SINGLE_LAST.value + 1):
            ctrl_type = ECtrlType(i)
            name = ctrl_type.name.lower()
            image = LoadImage(os.path.join(controls_dir, f"control_{name}.png"))
            feature = ExtractFeature_Control_Single(image)
            single_ctrls.append(f"{ImageHash(feature)}")
        print(f"Added {ECtrlType.NUM_CTRLS_SINGLE.value} ctrl hashs to database")
        self.data["ctrls"] = single_ctrls

        if cfg.DEBUG:
            CheckHashDistances("ctrls", features, name_func=lambda i: ECtrlType(i).name.lower())

            game_start_test_path = "temp/test/game_start_frame.png"
            image = LoadImage(game_start_test_path)
            feature = ExtractFeature_Control(image)
            ctrl_ids, dists = self.SearchByFeature(feature, EAnnType.CTRLS)
            LogDebug(info=f'{game_start_test_path}, {dists[0]=}, {ECtrlType(ctrl_ids[0]).name}')

            # Game over test
            from .tasks import GameOverTask
            image = LoadImage(os.path.join(cfg.debug_dir, f"GameOverTest.png"))
            main_content, valid = GameOverTask.CropMainContent(image)
            feature = ExtractFeature_Control(main_content)
            ctrl_ids, dists = self.SearchByFeature(feature, EAnnType.CTRLS)
            LogDebug(info=f"GameOverTest, {dists[0]=}, {ECtrlType(ctrl_ids[0]).name}")
            SaveImage(main_content, os.path.join(cfg.debug_dir, f"GameOverTest_MainContent.png"))


    def _UpdateActionCards(self, ctx: DatabaseUpdateContext):
        # sharables
        with open(os.path.join(cfg.cards_dir, "generated", "actions.csv"), 
                    mode='r', newline='', encoding='utf-8') as csv_file:
            csv_reader = csv.DictReader(csv_file)
            csv_data = [row for row in csv_reader]
        num_sharable = len(csv_data)
        # tokens
        with open(os.path.join(cfg.cards_dir, "generated", "tokens.csv"), 
                    mode='r', newline='', encoding='utf-8') as tokens_file:
            tokens_reader = csv.DictReader(tokens_file)
            for row in tokens_reader:
                row["id"] = int(row["id"]) + num_sharable
                csv_data.append(row)

        # left   = 70
        # width  = 100
        # top    = 320
        # height = 300
        # crop_box0 = CropBox(left, top, left + width, top + height)
        # cfg.feature_crop_box0 = ((crop_box0.left / 420, crop_box0.top / 720, crop_box0.width / 420, crop_box0.height / 720))
        # left   = 180
        # width  = 100
        # top    = 220
        # height = 200
        # crop_box1 = CropBox(left, top, left + width, top + height)
        # cfg.feature_crop_box1 = ((crop_box1.left / 420, crop_box1.top / 720, crop_box1.width / 420, crop_box1.height / 720))
        # left   = 222
        # width  = 100
        # top    = 508
        # height = 100
        # crop_box2 = CropBox(left, top, left + width, top + height)
        # cfg.feature_crop_box2 = ((crop_box2.left / 420, crop_box2.top / 720, crop_box2.width / 420, crop_box2.height / 720))

        handler = ActionCardHandler()
        handler.OnResize(CropBox(0, 0, 420, 720))

        arcane_legends = []

        action_cards_dir = os.path.join(cfg.cards_dir, "actions")
        num_actions = len(csv_data)
        actions  = [None] * num_actions
        ahashs   = [None] * num_actions
        dhashs   = [None] * num_actions
        for image_idx, row in enumerate(csv_data):
            card_id = int(row["id"])
            if image_idx < num_sharable:
                image_file = f'action_{card_id}_{row["zh-HANS"]}.png'
            else:
                image_file = f'tokens/token_{card_id - num_sharable}_{row["zh-HANS"]}.png'

            image_path = os.path.join(action_cards_dir, image_file)
            image = LoadImage(image_path)
            if image is None:
                Alert(info=f"Failed to load image: {image_path}")
        
            snapshot_path = os.path.join(
                cfg.assets_dir, "images", "snapshots", f"{card_id}.jpg")
            if ctx.save_image_assets:
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

            name_langs = {}
            for lang in range(ELanguage.NumELanguages.value):
                lang = ELanguage(lang)
                if lang == ELanguage.FollowSystem:
                    continue
                lang_name = lang.name.replace('_', '-')
                name_langs[lang_name] = row[lang_name].strip()

            action = {
                **name_langs,
                "type" : EActionCardType[row["type"]].value,
                "cost" : (cost, cost_type),
            }
            if action["type"] == EActionCardType.ArcaneLegend.value:
                arcane_legends.append((card_id, image))
            elif action["type"] == EActionCardType.Artifact.value:
                ctx.num_artifacts += 1

            ahashs[card_id]   = ahash
            dhashs[card_id]   = dhash
            actions[card_id]  = action

        print(f"Loaded {len(ahashs)} images from {action_cards_dir}")
        self.data["actions"] = actions


        # extras
        extra_cards_dir = os.path.join(action_cards_dir, "extras")
        extra_image_names = os.listdir(extra_cards_dir)
        num_extra_goldens = len(extra_image_names)
        num_arcane_legends = len(arcane_legends)
        num_extras = num_extra_goldens + num_arcane_legends * 2
        extras = [None] * num_extras
        ahash_extras = [None] * num_extras
        dhash_extras = [None] * num_extras

        for extra_image_name in extra_image_names:
            info = extra_image_name[:-4] # remove ".png"
            parts = info.split('_')
            extra_id  = int(parts[1])
            mapped_id = int(parts[3])
            if parts[2] == "token":
                mapped_id += num_sharable

            extra_path = os.path.join(action_cards_dir, "extras", extra_image_name)
            image = LoadImage(extra_path)
            if image is None:
                Alert(info=f"Failed to load image: {extra_path}")
        
            handler.frame_buffer = image
            ahash, dhash = handler.ExtractCardFeatures()
    
            extras[extra_id] = mapped_id
            ahash_extras[extra_id] = ahash
            dhash_extras[extra_id] = dhash

        boxes = [
            (-22, 0, 315, 540), # my
            (128, 50, 315, 540), # op
        ]
        for j, (left, top, width, height) in enumerate(boxes):
            for i, (mapped_id, image) in enumerate(arcane_legends):
                r = min(left + width, 420)
                b = min(top + height, 720)
                l = max(left, 0)
                t = max(top, 0)
                region = image[t:b, l:r]

                buffer = np.zeros((height, width, 4), dtype=image.dtype)
                if left < 0:
                    buffer[-top:, -left:] = region
                else:
                    buffer[:720-top, :420-left] = region
                buffer = cv2.resize(buffer, (420, 720), interpolation=cv2.INTER_LANCZOS4)
                # cv2.imshow("name", buffer)
                # cv2.waitKey(0)
                handler.frame_buffer = buffer
                ahash, dhash = handler.ExtractCardFeatures()

                extra_id = j * num_arcane_legends + i + num_extra_goldens
                extras[extra_id] = mapped_id
                ahash_extras[extra_id] = ahash
                dhash_extras[extra_id] = dhash

        print(f"Added {len(ahash_extras)} extra images = {num_extra_goldens} goldens + {num_arcane_legends} arcane legends * 2")

        ahashs += ahash_extras
        dhashs += dhash_extras
        self.data["extras"] = extras

        if cfg.DEBUG:
            # ahash
            CheckHashDistances("actions_a", ahashs, 
                lambda i: actions[i if i < num_actions else extras[i - num_actions]]["zh-HANS"])
            # dhash
            CheckHashDistances("actions_d", dhashs, 
                lambda i: actions[i if i < num_actions else extras[i - num_actions]]["zh-HANS"])

        if cfg.DEBUG_SAVE:
            card_id = 211
            image_file = f'action_{card_id}_{actions[card_id]["zh-HANS"]}.png'
            image = LoadImage(os.path.join(action_cards_dir, image_file))
            SaveImage(image, os.path.join(cfg.debug_dir, image_file))
            LogDebug(info=f"save {image_file} at {cfg.debug_dir}")

        ann_ahash = self.CreateAndSaveAnn(ahashs, EAnnType.ACTIONS_A)
        self.anns[EAnnType.ACTIONS_A.value] = ann_ahash

        ann_dhash = self.CreateAndSaveAnn(dhashs, EAnnType.ACTIONS_D)
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

        ctx.num_sharable_actions = num_sharable
        ctx.num_extra_goldens = num_extra_goldens
        ctx.num_arcane_legends = num_arcane_legends

    def _UpdateCharacters(self, ctx: DatabaseUpdateContext):
        with open(os.path.join(cfg.cards_dir, "generated", "characters.csv"), 
                    mode='r', newline='', encoding='utf-8') as csv_file:
            reader = csv.DictReader(csv_file)
            data = [row for row in reader]
        num_characters = len(data)

        handler = CharacterCardHandler()
        handler.OnResize(CropBox(0, 0, 420, 720))
        characters_dir = os.path.join(cfg.cards_dir, "characters")
        ahashs  = [None] * num_characters
        dhashs  = [None] * num_characters

        talent_to_character = {}
        characters = [None] * num_characters
        for idx, row in enumerate(data):
            card_id = int(row["id"])

            image_path = os.path.join(characters_dir, f"character_{card_id}_{row['zh-HANS']}.png")
            image = LoadImage(image_path)
            if image is None:
                Alert(info=f"Failed to load image: {image_path}")

            handler.frame_buffer = image
            ahash, dhash = handler.ExtractCardFeatures()
            ahashs[card_id] = ahash
            dhashs[card_id] = dhash
            # SaveImage(handler.feature_buffer, snapshot_path)

            name_langs = {}
            for lang in range(ELanguage.NumELanguages.value):
                lang = ELanguage(lang)
                if lang == ELanguage.FollowSystem:
                    continue
                lang_name = lang.name.replace('_', '-')
                name_langs[lang_name] = row[lang_name].strip()
                name_langs[lang_name + "_short"] = row[lang_name + "_short"].strip()

            character = {
                **name_langs,
                "element"    : EElementType[row["element"]].value,
                "is_monster" : True if row["is_monster"] == "1" else False,
            }
            characters[card_id] = character
            talent_id = int(row["talent_id"])
            talent_to_character[talent_id] = int(row["id"])

            if ctx.save_image_assets:
                src_file = os.path.join(
                    cfg.cards_dir, "avatars", f'avatar_{row["id"]}_{row["zh-HANS"]}.png'
                    )
                dst_file = os.path.join(
                    cfg.assets_dir, "images", "avatars", f'{row["id"]}.png'
                    )
                shutil.copy(src_file, dst_file)

        print(f"Loaded {num_characters} images from {characters_dir}")
        self.data["characters"] = characters
        self.data["talent_to_character"] = talent_to_character
        ctx.num_characters = num_characters

        if cfg.DEBUG:
            # ahash
            CheckHashDistances("characters_a", ahashs, lambda i: characters[i]["zh-HANS"])
            # dhash
            CheckHashDistances("characters_d", dhashs, lambda i: characters[i]["zh-HANS"])

        ann_ahash = self.CreateAndSaveAnn(ahashs, EAnnType.CHARACTERS_A)
        self.anns[EAnnType.CHARACTERS_A.value] = ann_ahash

        ann_dhash = self.CreateAndSaveAnn(dhashs, EAnnType.CHARACTERS_D)
        self.anns[EAnnType.CHARACTERS_D.value] = ann_dhash

    def _UpdateGeneratedEnums(self, ctx: DatabaseUpdateContext):
        eActions = [StringToVariableName(action["en-US"]) for action in self.data["actions"]]
        eCharacters = [StringToVariableName(character["en-US"]) for character in self.data["characters"]]

        indent = 0
        file = None
        def WriteLine(line):
            print(" " * (indent * 4) + line, file=file)

        ####################
        # c-sharp
        file = open(os.path.join(cfg.cards_dir, "..", "src", "LumiTracker.Config", "Enums.gen.cs"), 
                    mode='w', encoding='utf-8')
        WriteLine("// This file is generated. Do not modify.")
        WriteLine("")
        WriteLine("namespace LumiTracker.Config")
        WriteLine("{")
        indent += 1

        # actions
        WriteLine("public enum EActionCard : int")
        WriteLine("{")
        indent += 1
        for eAction in eActions:
            WriteLine(eAction + ",")
        # controls
        WriteLine(f"")
        WriteLine(f"NumActions,")
        WriteLine(f"NumSharables = {ctx.num_sharable_actions},")
        WriteLine(f"NumTokens = NumActions - NumSharables,")
        WriteLine(f"NumExtraGoldens = {ctx.num_extra_goldens},")
        WriteLine(f"NumArcaneLegends = {ctx.num_arcane_legends},")
        indent -= 1
        WriteLine("}")
        WriteLine("")

        # characters
        WriteLine("public enum ECharacterCard : int")
        WriteLine("{")
        indent += 1
        for eCharacter in eCharacters:
            WriteLine(eCharacter + ",")
        # controls
        WriteLine(f"")
        WriteLine(f"NumCharacters,")
        indent -= 1
        WriteLine("}")

        indent -= 1
        WriteLine("}")
        file.close()

        ####################
        # python
        file = open(os.path.join(cfg.cards_dir, "..", "src", "LumiTracker.Watcher", "watcher", "_enums_gen.py"), 
                    mode='w', encoding='utf-8')
        WriteLine("# This file is generated. Do not modify.")
        WriteLine("")
        WriteLine("import enum")
        WriteLine("")

        # actions
        WriteLine("class EActionCard(enum.Enum):")
        indent += 1
        WriteLine(f"{eActions[0]} = 0")
        for eAction in eActions[1:]:
            WriteLine(f"{eAction} = enum.auto()")
        # controls
        WriteLine(f"")
        WriteLine(f"NumActions = enum.auto()")
        WriteLine(f"NumSharables = {ctx.num_sharable_actions}")
        WriteLine(f"NumTokens = NumActions - NumSharables")
        WriteLine(f"NumExtraGoldens = {ctx.num_extra_goldens}")
        WriteLine(f"NumArcaneLegends = {ctx.num_arcane_legends}")
        indent -= 1
        WriteLine("")

        # characters
        WriteLine("class ECharacterCard(enum.Enum):")
        indent += 1
        WriteLine(f"{eCharacters[0]} = 0")
        for eCharacter in eCharacters[1:]:
            WriteLine(f"{eCharacter} = enum.auto()")
        # controls
        WriteLine(f"")
        WriteLine(f"NumCharacters = enum.auto()")
        indent -= 1
        WriteLine("")

        file.close()

    def _UpdateExtraInfos(self, ctx: DatabaseUpdateContext):
        # share code
        with open(os.path.join(cfg.cards_dir, "generated", "share_code.csv"), 
                    mode='r', newline='', encoding='utf-8') as share_code_file:
            share_code_reader = csv.DictReader(share_code_file)
            share_code_data = [row for row in share_code_reader]
        if len(share_code_data) != ctx.num_sharable_actions + ctx.num_characters:
            Alert(info=
                f"Share id number check failed: share_code.csv({len(share_code_data)}) != "
                f"num_sharable_actions({ctx.num_sharable_actions}) + num_characters({ctx.num_characters})")

        share_to_internal = [0] + [None] * len(share_code_data)
        for i, row in enumerate(share_code_data):
            share_id = int(row["share_id"])
            if share_id != i + 1:
                Alert(info=f"Share id consecutiveness check failed at share_id = {share_id}")
            internal_id = int(row["internal_id"])
            is_character = (int(row["is_character"]) == 1)
            if is_character:
                share_to_internal[share_id] = -(internal_id + 1)
            else:
                share_to_internal[share_id] = internal_id + 1

        self.data["share_to_internal"] = share_to_internal

        # artifacts
        with open(os.path.join(cfg.cards_dir, "generated", "artifacts.csv"), 
                    mode='r', newline='', encoding='utf-8') as artifacts_file:
            artifacts_reader = csv.DictReader(artifacts_file)
            artifacts_data = [row for row in artifacts_reader]
        if len(artifacts_data) != ctx.num_artifacts:
            Alert(info=f"Artifacts number check failed: artifacts.csv({len(artifacts_data)}) != actions.csv({ctx.num_artifacts})")

        artifacts_order = {}
        for i, row in enumerate(artifacts_data):
            internal_id = int(row["internal_id"])
            artifacts_order[internal_id] = i

        self.data["artifacts_order"] = artifacts_order

        # digits
        digit_hashs = [None] * 20
        for i in range(20):
            image = LoadImage(os.path.join(cfg.cards_dir, "digits", f"{i}.png"))
            image = cv2.cvtColor(image, cv2.COLOR_GRAY2BGRA)
            feature = ExtractFeature_Digit(image)
            digit_hashs[i] = feature
        ann_digits = self.CreateAndSaveAnn(digit_hashs, EAnnType.DIGITS)
        self.anns[EAnnType.DIGITS.value] = ann_digits
        if cfg.DEBUG:
            CheckHashDistances("digits", digit_hashs, name_func=lambda i: "")

        if ctx.save_image_assets:
            # cost images
            for name in ECostType.__members__:
                src_file = os.path.join(
                    cfg.cards_dir, "costs", f'{name}.png'
                    )
                dst_file = os.path.join(
                    cfg.assets_dir, "images", "costs", f'{name}.png'
                    )
                shutil.copy(src_file, dst_file)

            # empty image
            shutil.copy(os.path.join(cfg.cards_dir, 'empty.png'), 
                        os.path.join(cfg.assets_dir, "images", 'empty.png'))

    def _Update(self, ctx: DatabaseUpdateContext):
        self._UpdateControls(ctx)
        self._UpdateActionCards(ctx)
        self._UpdateCharacters(ctx)
        self._UpdateExtraInfos(ctx)
        self._UpdateGeneratedEnums(ctx)

        with open(os.path.join(cfg.database_dir, cfg.db_filename), 'w', encoding='utf-8') as f:
            json.dump(self.data, f, indent=None, ensure_ascii=False)
        
        with open("assets/config.json", 'w') as f:
            json.dump(vars(cfg), f, indent=2, ensure_ascii=False)

    def Load(self):
        n_anns = EAnnType.ANN_COUNT.value
        for i in range(n_anns):
            hash_size = GetHashSize(EAnnType(i))
            ann_index_len = hash_size * hash_size
            ann = AnnoyIndex(ann_index_len, cfg.ann_metric)
            ann_filename = f"{EAnnType(i).name.lower()}.ann"
            ann.load(os.path.join(cfg.database_dir, ann_filename))
            self.anns[i] = ann

        with open(os.path.join(cfg.database_dir, cfg.db_filename), 'r', encoding='utf-8') as f:
            self.data = json.load(f)
    
    def CreateAndSaveAnn(self, features, ann_type):
        hash_size = GetHashSize(ann_type)
        index_len = hash_size * hash_size
        ann = AnnoyIndex(index_len, cfg.ann_metric)
        for i in range(len(features)):
            ann.add_item(i, features[i])
        ann.build(cfg.ann_n_trees)
        ann_filename = f"{ann_type.name.lower()}.ann"
        ann.save(os.path.join(cfg.database_dir, ann_filename))
        return ann

    def SearchByFeature(self, feature, ann_type):
        ann = self.anns[ann_type.value]

        # !!!!! Must use a large n, or it may not find the optimal result !!!!!
        ids, dists = ann.get_nns_by_vector(feature, n=20, include_distances=True)
        return ids, dists
    
    def GetFeatureById(self, target_id, ann_type):
        ann = self.anns[ann_type.value]
        return np.array(ann.get_item_vector(target_id))

if __name__ == '__main__':
    import sys
    ctx = DatabaseUpdateContext()
    ctx.save_image_assets = (len(sys.argv) > 1 and sys.argv[1] == "image")

    db = Database()
    db._Update(ctx)
    print("Database Updated.")