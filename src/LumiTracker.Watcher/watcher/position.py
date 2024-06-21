from .enums import ETaskType, ERatioType

# left, top, width, height
POS = {
    ERatioType.E16_9: {
        ETaskType.GAME_START : ( 0.4470, 0.4400, 0.1045, 0.1110 ),
        ETaskType.MY_EVENT   : ( 0.1225, 0.1755, 0.1400, 0.4270 ),
        ETaskType.OP_EVENT   : ( 0.7380, 0.1755, 0.1400, 0.4270 ),
    },
    ERatioType.E16_10: {
        ETaskType.GAME_START : ( 0.4470, 0.4470, 0.1045, 0.0995 ),
        ETaskType.MY_EVENT   : ( 0.1490, 0.2285, 0.1305, 0.3565 ),
        ETaskType.OP_EVENT   : ( 0.7215, 0.2285, 0.1305, 0.3565 ),
    },
    ERatioType.E64_27: {
        ETaskType.GAME_START : ( 0.4605, 0.4400, 0.0780, 0.1100 ),
        ETaskType.MY_EVENT   : ( 0.2170, 0.1755, 0.1050, 0.4270 ),
        ETaskType.OP_EVENT   : ( 0.6785, 0.1755, 0.1050, 0.4270 ),
    },
    ERatioType.E43_18: {
        ETaskType.GAME_START : ( 0.4605, 0.4400, 0.0780, 0.1100 ),
        ETaskType.MY_EVENT   : ( 0.2195, 0.1755, 0.1045, 0.4275 ),
        ETaskType.OP_EVENT   : ( 0.6776, 0.1755, 0.1045, 0.4275 ),
    },
    ERatioType.E12_5: {
        ETaskType.GAME_START : ( 0.4605, 0.4400, 0.0780, 0.1100 ),
        ETaskType.MY_EVENT   : ( 0.2205, 0.1755, 0.1045, 0.4270 ),
        ETaskType.OP_EVENT   : ( 0.6765, 0.1755, 0.1045, 0.4270 ),
    },
}