Module Module1

    Private libraryDB As DataTable

    Sub Main(args As String())
        Dim dbPath As String
        Dim rootPath As String
        Dim outputPath As String
        Dim resp As ConsoleKeyInfo

        Console.WriteLine("Starting...")

        ' Set default paths
        dbPath = $"{System.Environment.GetEnvironmentVariable("localappdata")}\Plex Media Server\Plug-In Support\Databases\com.plexapp.plugins.library.db"
        rootPath = $"{Environment.GetEnvironmentVariable("localappdata")}\Plex Media Server\Metadata\Albums"
        outputPath = "AlbumArtAudit.csv"

        ' Override with config file values if present
        Try
            If Not String.IsNullOrEmpty(Configuration.ConfigurationManager.AppSettings("dbPath")) Then dbPath = Configuration.ConfigurationManager.AppSettings("dbPath")
        Catch ex As Exception
            Console.WriteLine("Unable to set custom dbPath from config file.  Default will be used.")
        End Try

        Try
            If Not String.IsNullOrEmpty(Configuration.ConfigurationManager.AppSettings("rootPath")) Then rootPath = Configuration.ConfigurationManager.AppSettings("rootPath")
        Catch ex As Exception
            Console.WriteLine("Unable to set custom rootPath from config file.  Default will be used.")
        End Try

        Try
            If Not String.IsNullOrEmpty(Configuration.ConfigurationManager.AppSettings("outputPath")) Then outputPath = Configuration.ConfigurationManager.AppSettings("outputPath")
        Catch ex As Exception
            Console.WriteLine("Unable to set custom outputPath from config file.  Default will be used.")
        End Try

        ' Set up datatable for lookup and report
        libraryDB = New DataTable
        With libraryDB.Columns
            .Add(New DataColumn("Artist", GetType(String)))
            .Add(New DataColumn("Album", GetType(String)))
            .Add(New DataColumn("URL", GetType(String)))
            .Add(New DataColumn("Size", GetType(String)))
        End With

        Try
            Console.Write("Reading Plex database... ")
            ReadLibrary(dbPath)
            Console.WriteLine($"{libraryDB.Rows.Count} albums in database")
            Console.WriteLine("")

        Catch ex As Exception
            Console.WriteLine($"Error reading plex database.  If the database is not located at {dbPath}, specify the custom path in the config file.")
            GoTo SKIPTOENDING
        End Try

        Try
            Console.Write("Reading album art on drive...    ")
            CheckFolders(rootPath)
            Console.WriteLine("")

        Catch ex As Exception
            Console.WriteLine($"Error reading album art.  The error is {ex.Message}.  If the artwork is not located at {rootPath}, you can specify a custom path in the config file.")
            GoTo SKIPTOENDING
        End Try

        Try
            Console.WriteLine($"Saving results to {outputPath}...")
            WriteToFile(outputPath)
            Console.WriteLine("")

            Console.WriteLine("Do you want to open the result file now? (Y/n)")
            Do
                resp = Console.ReadKey()
                Select Case True
                    Case resp.Key = ConsoleKey.Y Or resp.Key = ConsoleKey.Enter
                        Process.Start(outputPath)
                        Exit Do

                    Case resp.Key = ConsoleKey.N
                        Exit Do

                End Select

            Loop

        Catch ex As Exception
            Console.WriteLine($"Error saving the result file.  The error is {ex.Message}.  Verify you can write a file to the path: {outputPath}.")
            GoTo SKIPTOENDING

        End Try

SKIPTOENDING:
        Console.WriteLine("Complete.")

    End Sub

    Private Sub ReadLibrary(dbPath As String)
        Dim conn As SQLite.SQLiteConnection
        Dim cmd As SQLite.SQLiteCommand
        Dim dr As SQLite.SQLiteDataReader

        ' open Plex
        conn = New SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;")
        conn.Open()

        ' query database
        cmd = New SQLite.SQLiteCommand("select ar.title as Artist,al.title as Album,al.user_thumb_url as URL from metadata_items al join metadata_items ar on ar.id=al.parent_id where al.metadata_type=9", conn)
        dr = cmd.ExecuteReader()

        ' add albums to datatable
        Do While dr.Read
            libraryDB.Rows.Add(CStr(dr("Artist")), CStr(dr("Album")), CStr(dr("URL")))
        Loop

        ' cleaanup
        dr.Close()
        cmd.Dispose()
        conn.Close()
        conn.Dispose()

    End Sub

    Private Sub CheckFolders(rootPath As String)
        Dim root As IO.DirectoryInfo
        Dim subfolders() As IO.DirectoryInfo
        Dim guidPath As String
        Dim bmp As System.Drawing.Bitmap
        Dim dv As DataView
        Dim idx As Integer = 1

        ' set up lookup filter
        dv = New DataView(libraryDB)
        root = New IO.DirectoryInfo(rootPath)

        ' loop through Plex folder structure
        subfolders = root.GetDirectories
        For Each subFl As IO.DirectoryInfo In subfolders
            Console.Write(vbBack & vbBack & vbBack & $"{Math.Round(CDbl(idx / subfolders.Count) * 100).ToString("00")}%")

            For Each guidFl As IO.DirectoryInfo In subFl.GetDirectories
                guidPath = IO.Path.Combine(guidFl.FullName, "Contents\_combined\posters")

                If IO.Directory.Exists(guidPath) Then
                    For Each f As IO.FileInfo In New IO.DirectoryInfo(guidPath).GetFiles
                        ' look in db to see if this album art is being used (there may be both a lastFM version and a local version available on disk)
                        dv.RowFilter = $"url='metadata://posters/{f.Name}'"

                        If dv.Count > 0 Then
                            Try
                                ' check the size of the image file (it doesn't have an extension)
                                bmp = DirectCast(System.Drawing.Bitmap.FromFile(f.FullName), System.Drawing.Bitmap)
                                dv(0).Row.Item("Size") = $"{bmp.Width}x{bmp.Height}"
                                bmp.Dispose()

                            Catch ex As Exception
                                Console.WriteLine("")
                                Console.WriteLine($"Error processing image file {f.Name}")
                                Console.Write("   ")
                            End Try

                        End If ' Matched in database

                    Next ' Each potential poster

                End If ' posters folder exists

            Next ' Each guid folder

            idx += 1

        Next ' each hex digit foler

        dv.Dispose()

        Console.WriteLine("")

    End Sub

    Private Sub WriteToFile(outputFilename As String)
        Dim sb As New System.Text.StringBuilder

        ' CSV header row
        sb.AppendLine("""Artist"",""Album"",""Size""")

        ' Add row for each album
        For Each dr As DataRow In libraryDB.Rows
            sb.AppendLine($"""{dr("Artist")}"",""{dr("Album")}"",""{dr("Size")}""")
        Next

        'save to file
        IO.File.WriteAllText(outputFilename, sb.ToString)

    End Sub

End Module
