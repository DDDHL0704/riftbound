alter table game_events
    drop constraint if exists game_events_match_id_event_tick_event_order_key;
