from types import SimpleNamespace

POS = {
    "16:9": SimpleNamespace(
        start_screen_size = ( 0.1045, 0.1110 ),
        start_screen_pos  = ( 0.4470, 0.4400 ),
        event_screen_size = ( 0.1400, 0.4270 ),
        my_event_pos      = ( 0.1225, 0.1755 ),
        op_event_pos      = ( 0.7380, 0.1755 ),
        round_screen_size = ( 0.0380, 0.0300 ),
        round_screen_pos  = ( 0.4730, 0.5430 ),
    ),
    "16:10": SimpleNamespace(
        start_screen_size = ( 0.1045, 0.0995 ),
        start_screen_pos  = ( 0.4470, 0.4470 ),
        event_screen_size = ( 0.1305, 0.3565 ),
        my_event_pos      = ( 0.1490, 0.2285 ),
        op_event_pos      = ( 0.7215, 0.2285 ),
        round_screen_size = ( 0.0380, 0.0300 ),
        round_screen_pos  = ( 0.4730, 0.5430 ),
    ),
    "64:27": SimpleNamespace(
        start_screen_size = ( 0.0780, 0.1100 ),
        start_screen_pos  = ( 0.4605, 0.4400 ),
        event_screen_size = ( 0.1050, 0.4270 ),
        my_event_pos      = ( 0.2170, 0.1755 ),
        op_event_pos      = ( 0.6785, 0.1755 ),
        round_screen_size = ( 0.0380, 0.0300 ),
        round_screen_pos  = ( 0.4730, 0.5430 ),
    ),
    "43:18": SimpleNamespace(
        start_screen_size = ( 0.0780, 0.1100 ),
        start_screen_pos  = ( 0.4605, 0.4400 ),
        event_screen_size = ( 0.1045, 0.4275 ),
        my_event_pos      = ( 0.2195, 0.1755 ),
        op_event_pos      = ( 0.6776, 0.1755 ),
        round_screen_size = ( 0.0380, 0.0300 ),
        round_screen_pos  = ( 0.4730, 0.5430 ),
    ),
    "12:5": SimpleNamespace(
        start_screen_size = ( 0.0780, 0.1100 ),
        start_screen_pos  = ( 0.4605, 0.4400 ),
        event_screen_size = ( 0.1045, 0.4270 ),
        my_event_pos      = ( 0.2205, 0.1755 ),
        op_event_pos      = ( 0.6765, 0.1755 ),
        round_screen_size = ( 0.0380, 0.0300 ),
        round_screen_pos  = ( 0.4730, 0.5430 ),
    ),
}