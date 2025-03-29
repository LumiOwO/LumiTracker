from .enums import ERegionType, ERatioType, EGameEvent
from .config import LogInfo, LogWarning
import json

def GetRatioType(client_width, client_height):
    ratio = client_width / client_height
    EPSILON = 0.005
    if   abs( ratio - 16 / 9 ) < EPSILON:
        ratio_type = ERatioType.E16_9
    elif abs( ratio - 16 / 10) < EPSILON:
        ratio_type = ERatioType.E16_10
    elif abs( ratio - 64 / 27) < EPSILON:
        ratio_type = ERatioType.E64_27
    elif abs( ratio - 43 / 18) < EPSILON:
        ratio_type = ERatioType.E43_18
    elif abs( ratio - 12 / 5 ) < EPSILON:
        ratio_type = ERatioType.E12_5
    else:
        LogInfo(
            type=EGameEvent.UnsupportedRatio.name, 
            client_width=client_width, 
            client_height=client_height,
            )
        ratio_type = ERatioType.E16_9 # default

    return ratio_type

_REGIONS = None
with open("assets/regions.json", 'r', encoding='utf-8') as f:
    loaded_data = json.load(f)
    # Convert string keys to enum keys
    _REGIONS = {
        ERatioType[key]: {  # Convert outer key (ratio type)
            ERegionType[inner_key]: inner_value  # Convert inner key (region type)
            for inner_key, inner_value in value.items()
        }
        for key, value in loaded_data.items()
    }

class _RegionOfRatio:
    def __init__(self, ratio_type):
        self.ratio_type = ratio_type

    def __getitem__(self, region_type):
        regions = _REGIONS[self.ratio_type]
        if region_type in regions:
            return regions[region_type]
        else:
            LogWarning(
                info=f"Region not defined in this ratio, falling back to 16:9 values",
                ratio=self.ratio_type.name,
                region=region_type.name
                )
            return _REGIONS[ERatioType.E16_9][region_type]

class REGIONS:
    @classmethod
    def __class_getitem__(cls, ratio_type):
        return _RegionOfRatio(ratio_type)