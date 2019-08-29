Public Class PosterAudit
    Private Shared libraryDB As DataTable

    Shared Sub Audit(dbPath As String, rootPath As String, outputPath As String)
        Try
            Console.Write("Reading Plex database... ")
            ReadLibrary(dbPath)
            Console.WriteLine($"{libraryDB.Rows.Count} albums in database")
            Console.WriteLine("")

        Catch ex As Exception
            Console.WriteLine($"Error reading plex database.  If the database is not located at {dbPath}, specify the custom path in the config file.")
            Exit Sub
        End Try

        Try
            Console.Write("Reading album art on drive...    ")
            CheckFolders(rootPath)
            Console.WriteLine("")

        Catch ex As Exception
            Console.WriteLine($"Error reading album art.  The error is {ex.Message}.  If the artwork is not located at {rootPath}, you can specify a custom path in the config file.")
            Exit Sub
        End Try

        Try
            Console.WriteLine($"Saving results to {outputPath}...")
            WriteToFile(outputPath)
            Console.WriteLine("")

        Catch ex As Exception
            Console.WriteLine($"Error saving the result file.  The error is {ex.Message}.  Verify you can write a file to the path: {outputPath}.")
            Exit Sub

        End Try

    End Sub

    Shared Sub SetAllToLargest(dbPath As String, rootPath As String)
        Try
            Console.Write("Reading Plex database... ")
            ReadLibrary(dbPath)
            Console.WriteLine($"{libraryDB.Rows.Count} albums in database")
            Console.WriteLine("")

        Catch ex As Exception
            Console.WriteLine($"Error reading plex database.  If the database is not located at {dbPath}, specify the custom path in the config file.")
            Exit Sub
        End Try

        Try
            Console.Write("Scanning available posters...    ")
            ScanForBetterPosters(rootPath)
            Console.WriteLine("")

        Catch ex As Exception
            Console.WriteLine($"Error reading album art.  The error is {ex.Message}.  If the artwork is not located at {rootPath}, you can specify a custom path in the config file.")
            Exit Sub
        End Try

        Try
            Console.WriteLine("Updating database...")
            UpdatePosterURLsInDatabase(dbPath)
            Console.WriteLine("")

        Catch ex As Exception
            Console.WriteLine($"Error updating database.  The error is: {ex.Message}.")

        End Try

    End Sub

    Private Shared Sub ReadLibrary(dbPath As String)
        Dim conn As SQLite.SQLiteConnection
        Dim cmd As SQLite.SQLiteCommand
        Dim dr As SQLite.SQLiteDataReader

        ' Set up datatable for lookup and report
        libraryDB = New DataTable
        With libraryDB.Columns
            .Add(New DataColumn("ID", GetType(Integer)))
            .Add(New DataColumn("Artist", GetType(String)))
            .Add(New DataColumn("Album", GetType(String)))
            .Add(New DataColumn("URL", GetType(String)))
            .Add(New DataColumn("Hash", GetType(String)))
            .Add(New DataColumn("Size", GetType(String)))
            .Add(New DataColumn("SortSize", GetType(String)))
        End With

        ' open Plex
        conn = New SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;")
        conn.Open()

        ' query database
        cmd = New SQLite.SQLiteCommand("select al.id,ar.title as Artist,al.title as Album,al.user_thumb_url as URL,al.hash 
            from metadata_items al 
            join metadata_items ar 
            on ar.id=al.parent_id 
            where al.metadata_type=9", conn)
        dr = cmd.ExecuteReader()

        ' add albums to datatable
        Do While dr.Read
            libraryDB.Rows.Add(CInt(dr("ID")), CStr(dr("Artist")), CStr(dr("Album")), CStr(dr("URL")), CStr(dr("Hash")))
        Loop

        ' cleaanup
        dr.Close()
        cmd.Dispose()
        conn.Close()
        conn.Dispose()

    End Sub

    Private Shared Sub CheckFolders(rootPath As String)
        Dim bundleFolder As IO.DirectoryInfo
        Dim bmp As System.Drawing.Bitmap
        Dim m As System.Text.RegularExpressions.Match
        Dim count As Integer
        Dim pos As Integer

        count = libraryDB.Rows.Count

        For Each dr As DataRow In libraryDB.Rows
            Console.Write($"{vbBack & vbBack & vbBack & vbBack & ((CDbl(pos / count) * 100).ToString("000"))}%")

            pos += 1

            bundleFolder = New IO.DirectoryInfo($"{rootPath}\{CStr(dr("hash")).Substring(0, 1)}\{CStr(dr("hash")).Substring(1)}.bundle")
            If Not bundleFolder.Exists Then
                Console.WriteLine($"Bundle folder not found for {dr("Artist")} - {dr("Album")}")
                Continue For
            End If

            Try
                If CStr(dr("url")).StartsWith("upload", StringComparison.CurrentCultureIgnoreCase) Then
                    bmp = DirectCast(System.Drawing.Bitmap.FromFile($"{bundleFolder.FullName}\Uploads\posters\{CStr(dr("url")).Substring(17)}"), Drawing.Bitmap)

                ElseIf CStr(dr("url")).StartsWith("metadata", StringComparison.CurrentCultureIgnoreCase) Then
                    m = Text.RegularExpressions.Regex.Match(CStr(dr("url")), "metadata:\/\/posters\/([0-9a-z\.]+)_([a-f0-9]+)")
                    If m.Success Then
                        bmp = DirectCast(System.Drawing.Bitmap.FromFile($"{bundleFolder.FullName}\Contents\{m.Groups(1).Value}\posters\{m.Groups(2).Value}"), Drawing.Bitmap)
                    Else
                        Continue For
                    End If

                Else
                    Continue For

                End If

                dr.Item("Size") = $"{bmp.Width}x{bmp.Height}"
                dr.Item("SortSize") = $"{bmp.Width * bmp.Height}"

                bmp.Dispose()

            Catch ex As Exception
                Console.WriteLine($"Could not process existing poster for {dr("Artist")} - {dr("Album")}")

            End Try

        Next

    End Sub

    Private Shared Sub WriteToFile(outputFilename As String)
        Dim sb As New System.Text.StringBuilder

        ' CSV header row
        sb.AppendLine("""Artist"",""Album"",""Size"",""SortSize""")

        ' Add row for each album
        For Each dr As DataRow In libraryDB.Rows
            sb.AppendLine($"""{dr("Artist")}"",""{dr("Album")}"",""{dr("Size")}"",""{dr("SortSize")}""")
        Next

        'save to file
        IO.File.WriteAllText(outputFilename, sb.ToString)

    End Sub

    Private Shared Sub ScanForBetterPosters(rootPath As String)
        Dim bundleFolder As IO.DirectoryInfo
        Dim bmp As System.Drawing.Bitmap
        Dim m As System.Text.RegularExpressions.Match
        Dim count As Integer
        Dim pos As Integer
        Dim existingSize As Drawing.Size
        Dim bestSize As Drawing.Size
        Dim bestFile As IO.FileInfo
        Dim newURL As String

        count = libraryDB.Rows.Count

        For Each dr As DataRow In libraryDB.Rows
            Console.Write($"{vbBack & vbBack & vbBack & vbBack & ((CDbl(pos / count) * 100).ToString("000"))}%")
            pos += 1

            bundleFolder = New IO.DirectoryInfo($"{rootPath}\{CStr(dr("hash")).Substring(0, 1)}\{CStr(dr("hash")).Substring(1)}.bundle")
            If Not bundleFolder.Exists Then
                Console.WriteLine($"Bundle folder not found for {dr("Artist")} - {dr("Album")}")
                Continue For
            End If

            Try
                If CStr(dr("url")).StartsWith("upload", StringComparison.CurrentCultureIgnoreCase) Then
                    bmp = DirectCast(System.Drawing.Bitmap.FromFile($"{bundleFolder.FullName}\Uploads\posters\{CStr(dr("url")).Substring(17)}"), Drawing.Bitmap)

                ElseIf CStr(dr("url")).StartsWith("metadata", StringComparison.CurrentCultureIgnoreCase) Then
                    m = Text.RegularExpressions.Regex.Match(CStr(dr("url")), "metadata:\/\/posters\/([0-9a-z\.]+)_([a-f0-9]+)")
                    If m.Success Then
                        bmp = DirectCast(System.Drawing.Bitmap.FromFile($"{bundleFolder.FullName}\Contents\{m.Groups(1).Value}\posters\{m.Groups(2).Value}"), Drawing.Bitmap)
                    Else
                        Continue For
                    End If

                Else
                    Continue For

                End If

            Catch ex As Exception
                Console.WriteLine($"Could not process existing poster for {dr("Artist")} - {dr("Album")}")
                Continue For

            End Try

            existingSize = bmp.Size
            bestSize = bmp.Size
            bestFile = Nothing
            bmp.Dispose()

            If New IO.DirectoryInfo(IO.Path.Combine(bundleFolder.FullName, "Contents")).Exists Then
                For Each subFolder As IO.DirectoryInfo In New IO.DirectoryInfo(IO.Path.Combine(bundleFolder.FullName, "Contents")).GetDirectories
                    If subFolder.Name.StartsWith("_") Then Continue For

                    If New IO.DirectoryInfo(IO.Path.Combine(subFolder.FullName, "posters")).Exists Then
                        For Each f As IO.FileInfo In New IO.DirectoryInfo(IO.Path.Combine(subFolder.FullName, "posters")).GetFiles
                            Try
                                bmp = DirectCast(Drawing.Bitmap.FromFile(f.FullName), Drawing.Bitmap)
                                If bmp.Size.Width * bmp.Size.Height > bestSize.Width * bestSize.Height Then
                                    bestFile = f
                                    bestSize = bmp.Size
                                End If
                                bmp.Dispose()

                            Catch ex As Exception
                                Console.WriteLine($"Could not process alternative poster for {dr("Artist")} - {dr("Album")}")
                                Continue For

                            End Try

                        Next

                    End If

                Next

            End If

            If New IO.DirectoryInfo(IO.Path.Combine(bundleFolder.FullName, "Uploads")).Exists Then
                For Each f As IO.FileInfo In New IO.DirectoryInfo(IO.Path.Combine(bundleFolder.FullName, "Uploads", "posters")).GetFiles
                    Try
                        bmp = DirectCast(Drawing.Bitmap.FromFile(f.FullName), Drawing.Bitmap)
                        If bmp.Size.Width * bmp.Size.Height > bestSize.Width * bestSize.Height Then
                            bestFile = f
                            bestSize = bmp.Size
                        End If
                        bmp.Dispose()

                    Catch ex As Exception
                        Console.WriteLine($"Could not process alternative uploaded poster for {dr("Artist")} - {dr("Album")}")
                        Continue For

                    End Try

                Next

            End If

            If bestFile IsNot Nothing Then
                If bestFile.FullName.IndexOf("Uploads", StringComparison.CurrentCultureIgnoreCase) >= 0 Then
                    newURL = $"upload://posters/{bestFile.Name}"

                Else
                    newURL = $"metadata://posters/{bestFile.Directory.Parent.Name}_{bestFile.Name}"

                End If

                dr("url") = newURL
                dr("size") = $"{bestSize.Width}x{bestSize.Height}"

            End If

        Next

    End Sub

    Private Shared Sub UpdatePosterURLsInDatabase(dbPath As String)
        Dim dv As DataView
        Dim conn As SQLite.SQLiteConnection
        Dim cmd As SQLite.SQLiteCommand

        dv = New DataView(libraryDB)
        dv.RowFilter = "size is not null"

        Console.WriteLine($"{dv.Count} changes queued. ")

        If dv.Count > 0 Then
            ' open Plex
            conn = New SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;")
            conn.Open()

            Console.Write("Sending updates ")
            For Each item As DataRowView In dv
                Try
                    cmd = New SQLite.SQLiteCommand($"update metadata_items set user_thumb_url='{item("url")}' where id={item("id")}", conn)
                    cmd.ExecuteNonQuery()
                    cmd.Dispose()
                    Console.Write(".")

                Catch ex As Exception
                    Console.WriteLine("")
                    Console.WriteLine($"Error updating ID {item("ID")}: {ex.Message}")
                    Console.WriteLine("")
                End Try

            Next

            Console.WriteLine(" complete.")

            conn.Close()

        End If

        dv.Dispose()

    End Sub

End Class
