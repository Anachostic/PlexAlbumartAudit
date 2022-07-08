Public Class ArtistArt

    Shared Sub ExportArtistArt(dbPath As String, rootPath As String)
        Dim conn As SQLite.SQLiteConnection
        Dim cmd As SQLite.SQLiteCommand
        Dim dr As SQLite.SQLiteDataReader
        Dim dt As DataTable
        Dim bundleFolder As IO.DirectoryInfo
        Dim m As System.Text.RegularExpressions.Match
        Dim count As Integer
        Dim pos As Integer

        Try
            dt = New DataTable
            With dt.Columns
                .Add(New DataColumn("ID", GetType(String)))
                .Add(New DataColumn("Artist", GetType(String)))
                .Add(New DataColumn("Hash", GetType(String)))
                .Add(New DataColumn("URL", GetType(String)))
                .Add(New DataColumn("ArtistPath", GetType(String)))
            End With

            ' open Plex
            conn = New SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;")
            conn.Open()

            ' query database
            cmd = New SQLite.SQLiteCommand("select distinct artist.id,artist.title Artist,artist.hash,artist.user_thumb_url URL,
	                substr(p.file,1,instr(substr(p.file,instr(p.file,'\')+1),'\')+instr(p.file,'\')) ArtistPath
                from metadata_items artist
                join metadata_items album
                on album.parent_id=artist.id
                join metadata_items song
                on song.parent_id=album.id
                join media_items m
                on m.metadata_item_id=song.id
                join media_parts p
                on p.media_item_id=m.id
                where artist.metadata_type=8", conn)
            dr = cmd.ExecuteReader()

            ' add artists to datatable
            Do While dr.Read
                dt.Rows.Add(CStr(dr("ID")), CStr(dr("Artist")), CStr(dr("Hash")), CStr(dr("URL")), CStr(dr("ArtistPath")))
            Loop

            ' cleanup
            dr.Close()
            cmd.Dispose()
            conn.Close()
            conn.Dispose()

            count = dt.Rows.Count

            For Each row As DataRow In dt.Rows
                Console.Write($"{vbBack & vbBack & vbBack & vbBack & ((CDbl(pos / count) * 100).ToString("000"))}%")

                pos += 1

                bundleFolder = New IO.DirectoryInfo($"{rootPath}\{CStr(row("hash")).Substring(0, 1)}\{CStr(row("hash")).Substring(1)}.bundle")
                If Not bundleFolder.Exists Then
                    Console.WriteLine($"Bundle folder not found for {row("Artist")}")
                    Console.Write("   ")
                    Continue For
                End If

                If CStr(row("url")).StartsWith("upload", StringComparison.CurrentCultureIgnoreCase) Then
                    Try
                        IO.File.Copy($"{bundleFolder.FullName}\Uploads\posters\{CStr(row("url")).Substring(17)}", IO.Path.Combine(CStr(row("ArtistPath")), "artist.jpg"), True)

                    Catch ex As Exception
                        Console.WriteLine($"  Could not find image file for artist {row("Artist")}")
                        Console.Write("   ")
                        Continue For
                    End Try

                ElseIf CStr(row("url")).StartsWith("metadata", StringComparison.CurrentCultureIgnoreCase) Then
                    m = Text.RegularExpressions.Regex.Match(CStr(row("url")), "metadata:\/\/posters\/([0-9a-z\.]+)_([a-f0-9]+)")
                    If m.Success Then
                        Try
                            ' check combined
                            If IO.File.Exists($"{bundleFolder.FullName}\Contents\_combined\posters\{CStr(row("url")).Substring(19)}") Then
                                IO.File.Copy($"{bundleFolder.FullName}\Contents\_combined\posters\{CStr(row("url")).Substring(19)}", IO.Path.Combine(CStr(row("ArtistPath")), "artist.jpg"), True)

                            ElseIf IO.File.Exists($"{bundleFolder.FullName}\Contents\{m.Groups(1).Value}\posters\{m.Groups(2).Value}") Then
                                ' check source
                                IO.File.Copy($"{bundleFolder.FullName}\Contents\{m.Groups(1).Value}\posters\{m.Groups(2).Value}", IO.Path.Combine(CStr(row("ArtistPath")), "artist.jpg"), True)

                            Else
                                Console.WriteLine($"  Could not find image file for artist {row("Artist")}")
                            End If

                        Catch ex As Exception
                            Console.WriteLine($"  Could not find image file for artist {row("Artist")}")
                            Console.Write("   ")
                            Continue For
                        End Try
                    Else
                        Continue For
                    End If

                Else
                    Console.WriteLine($"  No artwork for artist {row("Artist")}")
                    Console.Write("   ")
                    Continue For

                End If

            Next

            dt.Dispose()

        Catch ex As Exception
            Console.WriteLine($"Error exporting artist posters.  The error is {ex.Message}.")
            Exit Sub

        End Try

    End Sub

    Shared Sub SetArtistArtToLocal(dbPath As String, rootPath As String)
        Dim conn As SQLite.SQLiteConnection = Nothing
        Dim cmd As SQLite.SQLiteCommand
        Dim dr As SQLite.SQLiteDataReader
        Dim dt As DataTable
        Dim bundleFolder As IO.DirectoryInfo
        Dim posterFile As IO.FileInfo
        Dim url As String
        Dim count As Integer
        Dim pos As Integer
        Dim triggers As Dictionary(Of String, String) = Nothing

        Try
            dt = New DataTable
            With dt.Columns
                .Add(New DataColumn("ID", GetType(String)))
                .Add(New DataColumn("Artist", GetType(String)))
                .Add(New DataColumn("Hash", GetType(String)))
                .Add(New DataColumn("URL", GetType(String)))
                .Add(New DataColumn("FilePath", GetType(String)))
            End With

            ' open Plex
            conn = New SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;")
            conn.Open()

            ' query database
            cmd = New SQLite.SQLiteCommand("select artist.id,artist.title Artist,artist.hash,artist.user_thumb_url URL,max(p.file) FilePath
                from metadata_items artist
                join metadata_items album
                on album.parent_id=artist.id
                join metadata_items song
                on song.parent_id=album.id
                join media_items m
                on m.metadata_item_id=song.id
                join media_parts p
                on p.media_item_id=m.id
                where artist.metadata_type=8
                group by artist.id,artist.title,artist.hash,artist.user_thumb_url", conn)

            dr = cmd.ExecuteReader()

            ' add artists to datatable
            Do While dr.Read
                dt.Rows.Add(CStr(dr("ID")), CStr(dr("Artist")), CStr(dr("Hash")), CStr(dr("URL")), CStr(dr("FilePath")))
            Loop

            ' cleanup
            dr.Close()
            cmd.Dispose()

            count = dt.Rows.Count

            triggers = Metadata.GetMetadataTriggers(dbPath)

            For Each row As DataRow In dt.Rows
                Console.Write($"{vbBack & vbBack & vbBack & vbBack & ((CDbl(pos / count) * 100).ToString("000"))}%")

                pos += 1

                bundleFolder = New IO.DirectoryInfo($"{rootPath}\{CStr(row("hash")).Substring(0, 1)}\{CStr(row("hash")).Substring(1)}.bundle\Contents\com.plexapp.agents.localmedia\posters")
                If Not bundleFolder.Exists Then
                    Console.WriteLine($"Local agent folder not found for {row("Artist")}")
                    Console.Write("   ")
                    Continue For
                End If

                posterFile = bundleFolder.EnumerateFiles.FirstOrDefault
                If posterFile Is Nothing Then
                    Console.WriteLine($"No artist art file found for {row("Artist")}")
                    Console.Write("   ")
                    Continue For
                End If

                url = $"metadata://posters/com.plexapp.agents.localmedia_{posterFile.Name}"

                Try
                    cmd = New SQLite.SQLiteCommand($"update metadata_items set user_thumb_url='{url}' where metadata_type=8 and hash='{CStr(row("Hash"))}'", conn)
                    cmd.ExecuteNonQuery()
                    cmd.Dispose()

                Catch ex As Exception
                    Console.WriteLine($"Could not update artist art for {row("Artist")}: {ex.Message}")
                    Console.Write("   ")
                    Continue For

                End Try

            Next

            dt.Dispose()

        Catch ex As Exception
            Console.WriteLine($"Error setting artist art to local.  The error is {ex.Message}.")
            Exit Sub

        Finally
            If triggers IsNot Nothing Then Metadata.RestoreMetadataTriggers(dbPath, triggers)

            If conn IsNot Nothing Then
                conn.Close()
                conn.Dispose()
            End If

        End Try


    End Sub

End Class
