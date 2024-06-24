from .enums import ETaskType, ERatioType

# left, top, width, height
POS = {
    # 1920 x 1080, 2560 x 1440
    ERatioType.E16_9: {
        ETaskType.GAME_START : ( 0.4470, 0.4400, 0.1045, 0.1110 ),
        ETaskType.MY_EVENT   : ( 0.1225, 0.1755, 0.1400, 0.4270 ),
        ETaskType.OP_EVENT   : ( 0.7380, 0.1755, 0.1400, 0.4270 ),
        ETaskType.GAME_OVER  : ( 0.4220, 0.4640, 0.1555, 0.0690 ),
        ETaskType.ROUND      : ( 0.4670, 0.5420, 0.0445, 0.0310 ),
    },
    # 1920 x 1200
    ERatioType.E16_10: {
        ETaskType.GAME_START : ( 0.4470, 0.4470, 0.1045, 0.0995 ),
        ETaskType.MY_EVENT   : ( 0.1490, 0.2285, 0.1305, 0.3565 ),
        ETaskType.OP_EVENT   : ( 0.7215, 0.2285, 0.1305, 0.3565 ),
        ETaskType.GAME_OVER  : ( 0.4220, 0.4675, 0.1555, 0.0620 ),
        ETaskType.ROUND      : ( 0.4670, 0.5370, 0.0445, 0.0280 ),
    },
    # 2560 × 1080, 2048 x 864
    ERatioType.E64_27: {
        ETaskType.GAME_START : ( 0.4605, 0.4400, 0.0780, 0.1100 ),
        ETaskType.MY_EVENT   : ( 0.2170, 0.1755, 0.1050, 0.4270 ),
        ETaskType.OP_EVENT   : ( 0.6785, 0.1755, 0.1050, 0.4270 ),
        ETaskType.GAME_OVER  : ( 0.4420, 0.4650, 0.1165, 0.0690 ),
        ETaskType.ROUND      : ( 0.4690, 0.5410, 0.0405, 0.0320 ),
    },
    # 3440 × 1440, 2150 x 900
    ERatioType.E43_18: {
        ETaskType.GAME_START : ( 0.4605, 0.4400, 0.0780, 0.1100 ),
        ETaskType.MY_EVENT   : ( 0.2195, 0.1755, 0.1045, 0.4275 ),
        ETaskType.OP_EVENT   : ( 0.6776, 0.1755, 0.1045, 0.4275 ),
        ETaskType.GAME_OVER  : ( 0.4420, 0.4640, 0.1165, 0.0690 ),
        ETaskType.ROUND      : ( 0.4680, 0.5422, 0.0405, 0.0325 ),
    },
    # 3840 x 1600, 1920 x 800
    ERatioType.E12_5: {
        ETaskType.GAME_START : ( 0.4605, 0.4400, 0.0780, 0.1100 ),
        ETaskType.MY_EVENT   : ( 0.2205, 0.1755, 0.1045, 0.4270 ),
        ETaskType.OP_EVENT   : ( 0.6765, 0.1755, 0.1045, 0.4270 ),
        ETaskType.GAME_OVER  : ( 0.4420, 0.4640, 0.1165, 0.0690 ),
        ETaskType.ROUND      : ( 0.4690, 0.5410, 0.0405, 0.0345 ),
    },
}