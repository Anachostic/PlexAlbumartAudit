Module Module1

    Private libraryDB As DataTable
    Private dbPath As String
    Private rootPath As String
    Private outputPath As String
    Private resp As ConsoleKeyInfo

    Sub Main(args As String())
        Console.WriteLine("Starting...")

        ' Set default paths
        dbPath = $"{System.Environment.GetEnvironmentVariable("localappdata")}\Plex Media Server\Plug-In Support\Databases\com.plexapp.plugins.library.db"

        ' Override with config file values if present
        Try
            If Not String.IsNullOrEmpty(Configuration.ConfigurationManager.AppSettings("dbPath")) Then dbPath = Configuration.ConfigurationManager.AppSettings("dbPath")
        Catch ex As Exception
            Console.WriteLine("Unable to set custom dbPath from config file.  Default will be used.")
        End Try

        ' parse arguments for action to take
        Select Case True
            Case args.Contains("-?") Or args.Contains("/?")
                ShowHelp()
                Exit Select

            Case args.Contains("-ex", StringComparer.CurrentCultureIgnoreCase) Or args.Contains("/ex", StringComparer.CurrentCultureIgnoreCase)
                PlaylistManagement.ExportPlaylists(dbPath)

            Case args.Contains("-rs", StringComparer.CurrentCultureIgnoreCase) Or args.Contains("/rs", StringComparer.CurrentCultureIgnoreCase)
                rootPath = $"{Environment.GetEnvironmentVariable("localappdata")}\Plex Media Server\Metadata\Albums"

                Try
                    If Not String.IsNullOrEmpty(Configuration.ConfigurationManager.AppSettings("rootPath")) Then rootPath = Configuration.ConfigurationManager.AppSettings("rootPath")
                Catch ex As Exception
                    Console.WriteLine("Unable to set custom rootPath from config file.  Default will be used.")
                End Try

                PosterAudit.SetAllToLargest(dbPath, rootPath)
                Console.WriteLine("Done scanning.  Press any key to exit.")
                Console.ReadKey()

            Case Else
                outputPath = "AlbumArtAudit.csv"
                rootPath = $"{Environment.GetEnvironmentVariable("localappdata")}\Plex Media Server\Metadata\Albums"

                ' Override with config file values if present
                Try
                    If Not String.IsNullOrEmpty(Configuration.ConfigurationManager.AppSettings("outputPath")) Then outputPath = Configuration.ConfigurationManager.AppSettings("outputPath")
                Catch ex As Exception
                    Console.WriteLine("Unable to set custom outputPath from config file.  Default will be used.")
                End Try

                Try
                    If Not String.IsNullOrEmpty(Configuration.ConfigurationManager.AppSettings("rootPath")) Then rootPath = Configuration.ConfigurationManager.AppSettings("rootPath")
                Catch ex As Exception
                    Console.WriteLine("Unable to set custom rootPath from config file.  Default will be used.")
                End Try

                PosterAudit.Audit(dbPath, rootPath, outputPath)
                'AuditAlbumArt()

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

        End Select

        Console.WriteLine("Complete.")

    End Sub

    Private Sub UpdateArtToLargest()

    End Sub

    Private Sub ShowHelp()
        Dim sb As New System.Text.StringBuilder

        With sb
            .AppendLine(My.Application.Info.AssemblyName.ToUpper & " ")
            .AppendLine("")
            .AppendLine(" /EX       Export playlists")
            .AppendLine(" /RS       Replace posters with largest available size")

            .AppendLine(" /?        Displays help")
        End With

        Console.WriteLine(sb.ToString)

    End Sub

End Module
