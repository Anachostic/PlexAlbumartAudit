select *
from metadata_items 
where metadata_type=15;

create table tmp_Sort(id int,sortOrder int)

insert into tmp_Sort
select g.id,row_number() over (ORDER BY artist.title,song.title)*1000 sortOrder
from play_queue_generators g
join metadata_items pl
on pl.id=g.playlist_id
join metadata_items song
on song.id=g.metadata_item_id
join metadata_items album
on album.id=song.parent_id
join metadata_items artist
on artist.id=album.parent_id
join media_items m
on m.metadata_item_id=song.id
join media_parts p
on p.media_item_id=m.id
where pl.id=21063

select * from tmp_Sort

update play_queue_generators set [order]=(
	select sortOrder from tmp_Sort s where s.id=play_queue_generators.id
	)
	
select pl.title playlist,song.title song, artist.title artist,p.file,p.duration/1000 duration,g.[order]
from play_queue_generators g
join metadata_items pl
on pl.id=g.playlist_id
join metadata_items song
on song.id=g.metadata_item_id
join metadata_items album
on album.id=song.parent_id
join metadata_items artist
on artist.id=album.parent_id
join media_items m
on m.metadata_item_id=song.id
join media_parts p
on p.media_item_id=m.id
where pl.id=21063
order by g.[order]

drop table tmp_Sort