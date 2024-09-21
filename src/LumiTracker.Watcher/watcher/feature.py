import numpy as np
import cv2

from .config import LogWarning, cfg, LogDebug
from .enums import EAnnType, EActionCard

def GetHashSize(ann_type):
    return cfg.hash_size
    # if ann_type == EAnnType.ACTIONS_A:
    #     return 10
    # elif ann_type == EAnnType.ACTIONS_D:
    #     return 10
    # elif ann_type == EAnnType.CTRLS:
    #     return 10
    # elif ann_type == EAnnType.DIGITS:
    #     return 10
    # else:
    #     return 10

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

def AHash(gray_image, hash_size=8):
    """
    Average Hash computation

    Implementation follows https://www.hackerfactor.com/blog/index.php?/archives/432-Looks-Like-It.html

    Step by step explanation: https://web.archive.org/web/20171112054354/https://www.safaribooksonline.com/blog/2013/11/26/image-hashing-with-python/ # noqa: E501

    @image must be a PIL instance.
    @mean how to determine the average luminescence. can try numpy.median instead.
    """
    gray_image = cv2.resize(gray_image, (hash_size, hash_size), interpolation=cv2.INTER_AREA)

    avg = np.median(gray_image)
    diff = gray_image > avg
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

def PHash_A(gray_image, hash_size=8):
    """
    Perceptual Hash computation.

    Implementation follows http://www.hackerfactor.com/blog/index.php?/archives/432-Looks-Like-It.html

    Reference: https://github.com/JohannesBuchner/imagehash/blob/master/imagehash/__init__.py

    image: gray image, dtype == float32
    """
    # highfreq_factor = 4
    # img_size = hash_size * highfreq_factor
    # gray_image = cv2.resize(gray_image, (img_size, img_size), interpolation=cv2.INTER_AREA)

    # resize
    gray_image = cv2.resize(gray_image, (hash_size, hash_size), interpolation=cv2.INTER_AREA)
    
    dct = cv2.dct(gray_image)

    dctlowfreq = dct[:hash_size, :hash_size]
    med = np.median(dctlowfreq)
    diff = dctlowfreq > med
    
    return ImageHash(diff)

def PHash_D(gray_image, hash_size=8):
    """
    Perceptual Hash computation.

    Implementation follows http://www.hackerfactor.com/blog/index.php?/archives/432-Looks-Like-It.html

    Reference: https://github.com/JohannesBuchner/imagehash/blob/master/imagehash/__init__.py

    image: gray image, dtype == float32
    """
    # highfreq_factor = 4
    # img_size = hash_size * highfreq_factor
    # gray_image = cv2.resize(gray_image, (img_size, img_size), interpolation=cv2.INTER_AREA)

    # resize
    gray_image = cv2.resize(gray_image, (hash_size, hash_size + 1), interpolation=cv2.INTER_AREA)
    
    dct = cv2.dct(gray_image)

    # dhash vertical
    dctlowfreq = dct[:hash_size + 1, :hash_size]
    diff = dctlowfreq[1:, :] > dctlowfreq[:-1, :]
    
    return ImageHash(diff)

def MultiPHash(gray_image, target_size, hash_size=8):
    """
    Perceptual Hash computation.

    Implementation follows http://www.hackerfactor.com/blog/index.php?/archives/432-Looks-Like-It.html

    Reference: https://github.com/JohannesBuchner/imagehash/blob/master/imagehash/__init__.py

    image: gray image, dtype == float32
    """
    # highfreq_factor = 4
    # img_size = hash_size * highfreq_factor
    # gray_image = cv2.resize(gray_image, (img_size, img_size), interpolation=cv2.INTER_AREA)

    # resize
    w, h = target_size
    interpolation = cv2.INTER_LINEAR if gray_image.shape[0] < h else cv2.INTER_AREA
    gray_image = cv2.resize(gray_image, (w, h), interpolation=interpolation)
    
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

    @property
    def center_x(self):
        return self.left + self.width // 2

    @property
    def center_y(self):
        return self.top + self.height // 2

    def __repr__(self):
        return f"CropBox([L={self.left}, T={self.top}, R={self.right}, B={self.bottom}], [W={self.width}, H={self.height}])"

    def Merge(self, other):
        self.left   = min(self.left  , other.left)
        self.top    = min(self.top   , other.top)
        self.right  = max(self.right , other.right)
        self.bottom = max(self.bottom, other.bottom)

    def Inside(self, other):
        """ Check whether this box is inside the other box. """
        return self.left >= other.left and self.top >= other.top and self.right <= other.right and self.bottom <= other.bottom

def ExtractFeature_Control(image):
    # preprocess
    # to gray image
    gray_image = cv2.cvtColor(image, cv2.COLOR_BGRA2GRAY)

    # histogram equalization
    gray_image = cv2.equalizeHist(gray_image)

    # to float buffer
    gray_image = gray_image.astype(np.float32) / 255.0

    hash_size = GetHashSize(EAnnType.CTRLS)
    feature = PHash_D(gray_image, hash_size=hash_size)
    feature = feature.hash.flatten()

    return feature

def ExtractFeature_Digit_Binalized(binary):
    # resize
    binary = cv2.resize(binary, (160, 160), interpolation=cv2.INTER_LANCZOS4)

    # to float buffer
    binary = binary.astype(np.float32) / 255.0

    hash_size = GetHashSize(EAnnType.DIGITS)
    feature = AHash(binary, hash_size=hash_size)
    feature = feature.hash.flatten()

    return feature

def ExtractFeature_Digit(image):
    # preprocess
    # to gray image
    gray_image = cv2.cvtColor(image, cv2.COLOR_BGRA2GRAY)

    # binalize
    _, binary = cv2.threshold(gray_image, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)

    return ExtractFeature_Digit_Binalized(binary)

def ExtractFeature_ActionCard(image):
    # preprocess
    # to gray image
    gray_image = cv2.cvtColor(image, cv2.COLOR_BGRA2GRAY)

    # histogram equalization
    gray_image = cv2.equalizeHist(gray_image)

    # to float buffer
    gray_image = gray_image.astype(np.float32) / 255.0

    hash_size = GetHashSize(EAnnType.ACTIONS_A)
    ahash, dhash = MultiPHash(gray_image, target_size=cfg.feature_image_size, hash_size=hash_size)
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

def CardCost(card_id, db):
    return db["actions"][card_id]["cost"][0]


class ActionCardHandler:
    def __init__(self):
        self.feature_buffer = None
        self.crop_cfgs      = (cfg.action_crop_box0, cfg.action_crop_box1, cfg.action_crop_box2)

        self.frame_buffer   = None
        self.region_buffer  = None
        self.crop_box       = None  # init when resize

    def ExtractCardFeatures(self):
        # Get action card region
        region_buffer = self.frame_buffer[
            self.crop_box.top  : self.crop_box.bottom, 
            self.crop_box.left : self.crop_box.right
        ]
        self.region_buffer = region_buffer

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
        features = ExtractFeature_ActionCard(self.feature_buffer)
        return features
    
    def RemapCardId(self, card_id, db):
        if card_id >= EActionCard.NumActions.value:
            card_id = db["extras"][card_id - EActionCard.NumActions.value]
        return card_id

    def Update(self, frame_buffer, db):
        self.frame_buffer = frame_buffer

        ahash, dhash = self.ExtractCardFeatures()
        card_ids_a, dists_a = db.SearchByFeature(ahash, EAnnType.ACTIONS_A)
        card_ids_d, dists_d = db.SearchByFeature(dhash, EAnnType.ACTIONS_D)

        card_id = -1
        dist    = max(dists_a[0], dists_d[0])
        def PackedResult():
            # if card_id >= 0:
            #     LogDebug(
            #         card_a=CardName(card_id_a, db),
            #         card_d=CardName(card_id_d, db),
            #         dists=(dists_a[:3], dists_d[:3]))
            return card_id, dist, (dists_a[:3], dists_d[:3])

        card_id_a = self.RemapCardId(card_ids_a[0], db)
        card_id_d = self.RemapCardId(card_ids_d[0], db)

        threshold = cfg.threshold
        strict_threshold = cfg.strict_threshold
        dist_a = dists_a[0]
        dist_d = dists_d[0]

        # if card_id_a == 119 or card_id_d == 119:
        #     import os
        #     image = self.region_buffer
        #     cv2.imwrite(os.path.join(cfg.debug_dir, "save", f"{ActionCardHandler.debug_cnt}.png"), image)
        #     ActionCardHandler.debug_cnt += 1
        
        if dist_d <= strict_threshold: # dhash is more sensitive, so check it first
            card_id = card_id_d
            dist    = dist_d
            return PackedResult()
        if dist_a <= strict_threshold:
            card_id = card_id_a
            dist    = dist_a
            return PackedResult()

        # Invalid if AHash detect different card from DHash
        # Note: Count Down To 3 may need extra logic
        if card_id_a != card_id_d:
            return PackedResult()
        # A too large distance may likely be noise
        if dist_a > threshold or dist_d > threshold:
            return PackedResult()
        # Invalid if 1st nearest AHash differs not much from 2nd nearest AHash
        if dists_a[1] - dists_a[0] <= 5:
            return PackedResult()

        card_id = card_id_a
        dist    = min(dists_a[0], dists_d[0])
        return PackedResult()
    
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
