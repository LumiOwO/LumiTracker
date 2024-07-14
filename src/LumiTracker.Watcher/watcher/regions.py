from .enums import ERegionType, ERatioType

# left, top, width, height
REGIONS = {
    # 1920 x 1080, 2560 x 1440
    ERatioType.E16_9: {
        ERegionType.GAME_START : ( 0.4470, 0.4400, 0.1045, 0.1110 ),
        ERegionType.MY_PLAYED  : ( 0.1225, 0.1755, 0.1400, 0.4270 ),
        ERegionType.OP_PLAYED  : ( 0.7380, 0.1755, 0.1400, 0.4270 ),
        ERegionType.GAME_OVER  : ( 0.4220, 0.4240, 0.1555, 0.1190 ),
        ERegionType.ROUND      : ( 0.4670, 0.5420, 0.0445, 0.0310 ),
        ERegionType.CENTER     : ( 0.1000, 0.2600, 0.8000, 0.4800 ),
    },
    # 1920 x 1200
    ERatioType.E16_10: {
        ERegionType.GAME_START : ( 0.4470, 0.4470, 0.1045, 0.0995 ),
        ERegionType.MY_PLAYED  : ( 0.1490, 0.2285, 0.1305, 0.3565 ),
        ERegionType.OP_PLAYED  : ( 0.7215, 0.2285, 0.1305, 0.3565 ),
        ERegionType.GAME_OVER  : ( 0.4220, 0.4240, 0.1555, 0.1190 ),
        ERegionType.ROUND      : ( 0.4670, 0.5370, 0.0445, 0.0280 ),
        ERegionType.CENTER     : ( 0.1000, 0.2600, 0.8000, 0.4800 ),
    },
    # 2560 × 1080, 2048 x 864
    ERatioType.E64_27: {
        ERegionType.GAME_START : ( 0.4605, 0.4400, 0.0780, 0.1100 ),
        ERegionType.MY_PLAYED  : ( 0.2170, 0.1755, 0.1050, 0.4270 ),
        ERegionType.OP_PLAYED  : ( 0.6785, 0.1755, 0.1050, 0.4270 ),
        ERegionType.GAME_OVER  : ( 0.4420, 0.4250, 0.1165, 0.1190 ),
        ERegionType.ROUND      : ( 0.4690, 0.5410, 0.0405, 0.0320 ),ERegionType.CENTER     : ( 0.1000, 0.2600, 0.8000, 0.4800 ),
    },
    # 3440 × 1440, 2150 x 900
    ERatioType.E43_18: {
        ERegionType.GAME_START : ( 0.4605, 0.4400, 0.0780, 0.1100 ),
        ERegionType.MY_PLAYED  : ( 0.2195, 0.1755, 0.1045, 0.4275 ),
        ERegionType.OP_PLAYED  : ( 0.6776, 0.1755, 0.1045, 0.4275 ),
        ERegionType.GAME_OVER  : ( 0.4420, 0.4240, 0.1165, 0.1190 ),
        ERegionType.ROUND      : ( 0.4680, 0.5422, 0.0405, 0.0325 ),
        ERegionType.CENTER     : ( 0.1000, 0.2600, 0.8000, 0.4800 ),
    },
    # 3840 x 1600, 1920 x 800
    ERatioType.E12_5: {
        ERegionType.GAME_START : ( 0.4605, 0.4400, 0.0780, 0.1100 ),
        ERegionType.MY_PLAYED  : ( 0.2205, 0.1755, 0.1045, 0.4270 ),
        ERegionType.OP_PLAYED  : ( 0.6765, 0.1755, 0.1045, 0.4270 ),
        ERegionType.GAME_OVER  : ( 0.4420, 0.4240, 0.1165, 0.1190 ),
        ERegionType.ROUND      : ( 0.4690, 0.5410, 0.0405, 0.0345 ),
        ERegionType.CENTER     : ( 0.1000, 0.2600, 0.8000, 0.4800 ),
    },
}