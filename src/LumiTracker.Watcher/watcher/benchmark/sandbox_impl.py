import cv2
import numpy as np
from ..feature import ActionCardHandler, MultiPHash, GetHashSize
from ..enums import EAnnType
from ..config import cfg

class ExperimentalActionCardHandler(ActionCardHandler):
    """
    Agent Sandbox for optimizing the feature extraction algorithm.
    """
    def __init__(self):
        super().__init__()
        
    def GetAnnTypes(self):
        return EAnnType.ACTIONS_A, EAnnType.ACTIONS_D

    def RemapCardId(self, card_id, db, dist, threshold, strict_threshold):
        from ..feature import EActionCard
        if not EActionCard.IsExtra(card_id):
            return card_id

        if EActionCard.IsExtraGolden(card_id):
            return db["extras"][card_id - EActionCard.NumActions.value]

        if dist > strict_threshold:
            return -1

        return db["extras"][card_id - EActionCard.NumActions.value]

    def ExtractFeatures(self, feature_buffer):
        # to gray image
        gray_image = cv2.cvtColor(feature_buffer, cv2.COLOR_BGRA2GRAY)

        # Do not use equalizeHist! It shifts the entire distribution when a local glare appears.
        # Just normalize the pixel values safely to 0.0 - 1.0
        gray_image = cv2.normalize(gray_image, np.zeros_like(gray_image), 0, 255, cv2.NORM_MINMAX)
        gray_image = gray_image.astype(np.float32) / 255.0

        hash_size = GetHashSize(EAnnType.ACTIONS_A)
        ahash, dhash = MultiPHash(gray_image, target_size=cfg.feature_image_size, hash_size=hash_size)
        
        return ahash.hash.flatten(), dhash.hash.flatten()

    def AllowEarlyReturn(self, card_id):
        from ..feature import EActionCard
        return not EActionCard.IsExtra(card_id)
