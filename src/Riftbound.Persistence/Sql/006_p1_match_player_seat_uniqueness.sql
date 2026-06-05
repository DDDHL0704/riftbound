do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conrelid = 'match_players'::regclass
          and conname = 'match_players_match_id_seat_key'
    ) then
        alter table match_players
            add constraint match_players_match_id_seat_key unique (match_id, seat);
    end if;
end $$;
