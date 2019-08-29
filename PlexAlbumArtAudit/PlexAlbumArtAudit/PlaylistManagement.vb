Public Class PlaylistManagement

    Shared Sub ExportPlaylists(dbPath As String)
        Dim conn As SQLite.SQLiteConnection
        Dim cmd As SQLite.SQLiteCommand
        Dim dr As SQLite.SQLiteDataReader
        Dim dt As DataTable
        Dim dv As DataView
        Dim sb As System.Text.StringBuilder

        Try
            dt = New DataTable
            With dt.Columns
                .Add(New DataColumn("Playlist", GetType(String)))
                .Add(New DataColumn("Artist", GetType(String)))
                .Add(New DataColumn("Song", GetType(String)))
                .Add(New DataColumn("File", GetType(String)))
                .Add(New DataColumn("Duration", GetType(String)))
            End With

            ' open Plex
            conn = New SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;")
            conn.Open()

            ' query database
            cmd = New SQLite.SQLiteCommand("select pl.title playlist,song.title song, artist.title artist,p.file,p.duration/1000 duration
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
                order by pl.title,g.[order]", conn)
            dr = cmd.ExecuteReader()

            ' add albums to datatable
            Do While dr.Read
                dt.Rows.Add(CStr(dr("PlayList")), CStr(dr("Artist")), CStr(dr("Song")), CStr(dr("File")), CStr(dr("Duration")))
            Loop

            ' cleaanup
            dr.Close()
            cmd.Dispose()
            conn.Close()
            conn.Dispose()

            dv = New DataView(dt)
            For Each row As DataRow In dv.ToTable(True, New String() {"Playlist"}).Rows
                Console.WriteLine($"Saving Playlist: {row.Item("PlayList")}")
                dv.RowFilter = $"Playlist='{row.Item("PlayList").ToString.Replace("'", "''")}'"

                sb = New Text.StringBuilder
                sb.AppendLine("#EXTM3U")
                For Each songRow As DataRowView In dv
                    sb.AppendLine($"#EXTINF:{songRow("Duration")},{songRow("Artist")} - {songRow("Song")}")
                    sb.AppendLine($"{songRow("File")}")
                Next

                IO.File.WriteAllText(CleanFileName(row.Item("Playlist").ToString & ".m3u"), sb.ToString)

            Next

            dv.Dispose()
            dt.Dispose()

        Catch ex As Exception
            Console.WriteLine($"Error exporting playlists.  The error is {ex.Message}.")
            Exit Sub

        End Try

    End Sub

    Private Shared Function CleanFileName(filename As String) As String
        For Each c As Char In IO.Path.GetInvalidFileNameChars
            filename = filename.Replace(c, "_")
        Next

        Return filename

    End Function

End Class
