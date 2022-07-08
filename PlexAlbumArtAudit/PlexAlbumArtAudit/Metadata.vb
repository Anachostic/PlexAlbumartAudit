Public Class Metadata
    Shared Function GetMetadataTriggers(dbPath As String) As Dictionary(Of String, String)
        Dim conn As SQLite.SQLiteConnection
        Dim cmd As SQLite.SQLiteCommand
        Dim dr As SQLite.SQLiteDataReader
        Dim triggers As New Dictionary(Of String, String)

        ' open Plex
        conn = New SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;")
        conn.Open()

        ' query database
        Console.WriteLine("Retrieving triggers... ")
        cmd = New SQLite.SQLiteCommand("SELECT name, sql FROM sqlite_master WHERE type='trigger' AND tbl_name='metadata_items' AND name LIKE '%update%'", conn)
        dr = cmd.ExecuteReader()

        ' add triggers to dictionary
        Do While dr.Read
            triggers.Add(CStr(dr("name")), CStr(dr("sql")))
        Loop

        ' cleaanup
        dr.Close()
        cmd.Dispose()

        'Drop triggers
        Console.Write("Dropping triggers... ")
        For Each key In triggers.Keys
            Try
                cmd = New SQLite.SQLiteCommand($"DROP TRIGGER {key}", conn)
                cmd.ExecuteNonQuery()
                cmd.Dispose()

            Catch ex As Exception
                Console.WriteLine("")
                Console.WriteLine($"Error dropping trigger {key}: {ex.Message}")
                Console.WriteLine("")
            End Try

        Next

        Console.WriteLine(" complete.")

        conn.Close()
        conn.Dispose()

        Return triggers

    End Function

    Shared Sub RestoreMetadataTriggers(dbPath As String, triggers As Dictionary(Of String, String))
        Dim conn As SQLite.SQLiteConnection
        Dim cmd As SQLite.SQLiteCommand

        If triggers.Count > 0 Then
            ' open Plex
            conn = New SQLite.SQLiteConnection($"Data Source={dbPath};Version=3;")
            conn.Open()

            Console.Write("Restoring triggers... ")
            For Each key In triggers.Keys
                Try
                    cmd = New SQLite.SQLiteCommand(triggers(key), conn)
                    cmd.ExecuteNonQuery()
                    cmd.Dispose()

                Catch ex As Exception
                    Console.WriteLine("")
                    Console.WriteLine($"Error creating trigger: {ex.Message}")
                    Console.WriteLine("")
                End Try

            Next

            Console.WriteLine(" complete.")

            conn.Close()
            conn.Dispose()

        End If

    End Sub

End Class
