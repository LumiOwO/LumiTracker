import logging

class cfg:
    DEBUG               = False
    debug_dir           = "temp"
    SKIP_FRAMES         = 0
    LOG_INTERVAL        = 3

    proc_name           = "YuanShen.exe"
    proc_watch_interval = 1

    # file paths
    database_dir        = "assets/database"
    events_ann_filename = "events.ann"
    db_filename         = "db.json"
    cards_dir           = "cards"

    # feature extraction
    hash_size           = 8
    threshold           = 18
    ann_metric          = "hamming"   # ["angular", "euclidean", "manhattan", "hamming", "dot"]
    ann_n_trees         = 10
    ann_index_len       = hash_size * hash_size * 2

    # (y, x), same as screen coordinate
    # namely (width, height)
    start_screen_size = (0.1445, 0.2847) # centered
    event_screen_size = (0.1400, 0.4270)
    my_event_pos      = (0.1225, 0.1755)
    op_event_pos      = (0.7380, 0.1755)


logging.basicConfig(level=logging.DEBUG if cfg.DEBUG else logging.INFO,
                    format='%(asctime)s - [%(levelname)s] - %(message)s')