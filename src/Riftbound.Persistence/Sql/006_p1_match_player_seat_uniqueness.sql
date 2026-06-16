do $$
begin
    if to_regclass('public.match_players') is not null
       and not exists (
        select 1
        from pg_constraint
        where conrelid = to_regclass('public.match_players')
          and conname = 'match_players_match_id_seat_key'
    ) then
        with ranked as (
            select ctid,
                   row_number() over (
                       partition by match_id, seat
                       order by updated_at desc, joined_at desc, player_id
                   ) as duplicate_rank
            from match_players
        )
        delete from match_players
        using ranked
        where match_players.ctid = ranked.ctid
          and ranked.duplicate_rank > 1;

        alter table match_players
            add constraint match_players_match_id_seat_key unique (match_id, seat);
    end if;
end $$;
