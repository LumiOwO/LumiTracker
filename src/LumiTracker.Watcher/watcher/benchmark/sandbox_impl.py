from ..feature import ActionCardHandler, ExtractFeature_ActionCard, FeatureDistance
from ..enums import EAnnType

class ExperimentalActionCardHandler(ActionCardHandler):
    """
    Agent Sandbox for optimizing the feature extraction algorithm.
    
    You can safely overwrite this class to test new preprocessing steps, 
    crop regions, or hashing configurations without modifying the 
    production `feature.py` pipeline.
    """
    def __init__(self):
        super().__init__()
        # You can override self.crop_cfgs here if needed
        # e.g., self.crop_cfgs = ((0.1, 0.1, 0.2, 0.2), ...)
        
    def GetAnnTypes(self):
        return EAnnType.ACTIONS_A, EAnnType.ACTIONS_D

    def RemapCardId(self, card_id, db, dist, threshold, strict_threshold):
        # Default behavior: use the same logic as production
        from ..feature import EActionCard, EGameEvent
        if not EActionCard.IsExtra(card_id):
            return card_id

        if EActionCard.IsExtraGolden(card_id):
            return db["extras"][card_id - EActionCard.NumActions.value]

        if dist > strict_threshold:
            return -1

        # Ignore game event checks in sandbox for pure accuracy testing
        # Or mock it if necessary. Here we just map all ArcaneLegends.
        return db["extras"][card_id - EActionCard.NumActions.value]

    def ExtractFeatures(self, feature_buffer):
        """
        The core function to optimize.
        Takes the merged cropped regions (feature_buffer) and returns two hashes.
        """
        # Default behavior: calls production algorithm
        return ExtractFeature_ActionCard(feature_buffer)

    def AllowEarlyReturn(self, card_id):
        from ..feature import EActionCard
        return not EActionCard.IsExtra(card_id)

    # You can also override ExtractCardFeatures(self) to change how 
    # the frame_buffer is cropped and merged entirely.
