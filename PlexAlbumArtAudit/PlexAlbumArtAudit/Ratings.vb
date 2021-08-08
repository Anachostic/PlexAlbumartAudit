Public Class Ratings

    Shared Sub SetRatingFromFile(dbPath As String, rootPath As String, scale As Integer)
        Dim conn As SQLite.SQLiteConnection = Nothing
        Dim cmdRead As SQLite.SQLiteCommand
        Dim cmdWrite As SQLite.SQLiteCommand
        Dim dr As SQLite.SQLiteDataReader
        Dim id As Integer
        Dim fileGuid As String
        Dim dbRating As Integer
        Dim fileRating As Integer
        Dim scaledRating As Integer
        Dim ff As FlacLibSharp.FlacFile

        Console.WriteLine("")
        Console.WriteLine($"Adding ratings from files in {rootPath}")


        Try
            ' open Plex 
            conn = New SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;")
            conn.Open()

            For Each file As String In IO.Directory.EnumerateFiles(rootPath, "*.flac")
                Console.Write($"File: {IO.Path.GetFileName(file)}: ")
                Try
                    ' query database
                    cmdRead = New SQLite.SQLiteCommand($"select s.id,i.guid,s.rating
                    from media_parts mp
                    join media_items mi 
                    on mi.id=mp.media_item_id
                    join metadata_items i
                    on i.id=mi.metadata_item_id
                    left join metadata_item_settings s
                    on s.guid=i.guid 
                    where mp.file='{file.Replace("'", "''")}'", conn)

                    dr = cmdRead.ExecuteReader

                    ' If the file is in Plex, get its rating
                    If dr.Read Then
                        If Not IsDBNull(dr("id")) Then
                            id = CInt(dr("id"))
                            fileGuid = CStr(dr("guid"))
                            If Not IsDBNull(dr("rating")) Then
                                dbRating = CInt(dr("rating"))
                            Else
                                dbRating = 0
                            End If

                        Else
                            id = 0
                            fileGuid = CStr(dr("guid"))
                            dbRating = 0
                        End If

                        dr.Close()

                        Console.Write($" Rating in db={dbRating}, Rating in file=")

                        ' get the rating from the file
                        ff = New FlacLibSharp.FlacFile(file)
                        If Not String.IsNullOrEmpty(ff.VorbisComment("RATING MM").Value.ToString) Then
                            fileRating = CInt(ff.VorbisComment("RATING MM").Value.ToString)
                            scaledRating = CInt(fileRating * (10 / scale))

                            Console.Write(scaledRating)
                        Else
                            ' no rating, nothing to update
                            Console.WriteLine("none, Skipping.")
                            Continue For
                        End If

                        ' if they are different, update Plex to match
                        If scaledRating <> dbRating Then
                            If id = 0 Then
                                cmdWrite = New SQLite.SQLiteCommand($"insert into metadata_item_settings(account_id,guid,rating,view_count,created_at,updated_at,extra_data)
                                values(1,'{fileGuid.Replace("'", "''")}',{scaledRating},0,'{Now.ToShortDateString}','{Now.ToShortDateString}','')", conn)
                                Console.WriteLine(", Adding.")

                            Else
                                cmdWrite = New SQLite.SQLiteCommand($"update metadata_item_settings set rating={scaledRating},updated_at='{Now.ToShortDateString}' where id={id}", conn)
                                Console.WriteLine(", Updating.")

                            End If

                            cmdWrite.ExecuteNonQuery()
                            cmdWrite.Dispose()

                        Else
                            Console.WriteLine(", Ignoring.")

                        End If

                    Else
                        Console.WriteLine(" not found in Plex")

                    End If

                Finally
                    cmdRead.Dispose()

                End Try

            Next


        Catch ex As Exception
            Console.WriteLine($"Error processing {rootPath}: {ex.Message}")
            Exit Sub

        Finally
            If conn IsNot Nothing Then
                conn.Close()
                conn.Dispose()
            End If

        End Try

        For Each dir As String In IO.Directory.GetDirectories(rootPath)
            SetRatingFromFile(dbPath, dir, scale)
        Next

    End Sub

End Class
