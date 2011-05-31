Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Linq
Imports System.Net
Imports System.Threading
Imports Microsoft.WindowsAzure
Imports Microsoft.WindowsAzure.Diagnostics
Imports Microsoft.WindowsAzure.ServiceRuntime
Imports Microsoft.WindowsAzure.StorageClient
Imports System.Reflection
Imports System.IO
Imports System.Text
Imports System.Data.Services 'A reference needs to be added to your project for this
Imports System.Security.Cryptography.X509Certificates
Imports System.Collections.Specialized


' Make sure that your service definition has the startup task defined in this solution.
' Make sure the batch file, installer and executables that are included in this project have "copy to output directory" set to "copy always"


Public Class WorkerRole
    Inherits RoleEntryPoint

    Private Shared streamError As StreamWriter = Nothing
    'Private Shared netErrorFile As String = ""
    Private Shared netOutput As StringBuilder = Nothing
    'Private Shared errorRedirect As Boolean = False
    Private Shared errorsWritten As Boolean = False
    Private Shared _timeDBBackup As System.Timers.Timer

    Public Overrides Sub Run()
        _timeDBBackup = New System.Timers.Timer(24 * 60 * 60 * 1000) 'Backup every 24 hours
        AddHandler _timeDBBackup.Elapsed, AddressOf timeDBBackup_Elapsed
        _timeDBBackup.Enabled = True
        Thread.Sleep(Timeout.Infinite)
    End Sub

    Public Overrides Function OnStart() As Boolean

        ' Set the maximum number of concurrent connections 
        ServicePointManager.DefaultConnectionLimit = 12

        ' For information on handling configuration changes
        ' see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

        Return MyBase.OnStart()

    End Function

    Private Shared Function BackupDB() As String
        Dim strLocalStoragePath As String = GetLocalDirectory("output") & "\"
        Dim sUserName As String = "username@xxxxxxxxxx"
        Dim sPassword As String = "1234567890AZ"
        Dim sServer As String = "xxxxxxxxxx.database.windows.net"
        Dim sDatabaseName As String = "mydatabase"
        Dim strOutput As String = ""
        Dim strCurrentTable As String = ""
        Dim intTableErrorCount As Integer = 0
        Try
            Dim intTableCount As Integer = 0
            With New WorkerRole1.DataSet1TableAdapters.tablesTableAdapter 'You need to configure the connection settings for this table in the project properties settings
                For Each rwTable As WorkerRole1.DataSet1.tablesRow In .GetData()
                    strCurrentTable = rwTable.name
                    Try
                        intTableCount += 1
                        ExecuteBCP(" " & sDatabaseName & ".dbo." & rwTable.name & " format nul -x -f " & strLocalStoragePath & rwTable.name & "-n.xml -n -S " & sServer & " -U" & sUserName & " -P" & sPassword)
                        ExecuteBCP(" " & sDatabaseName & ".dbo." & rwTable.name & " out " & strLocalStoragePath & rwTable.name & ".dat -n -b 1000 -U" & sUserName & " -P" & sPassword & " -S " & sServer & " -e errorlog-" & rwTable.name & ".txt")
                        Dim myMeta As New System.Collections.Specialized.NameValueCollection
                        myMeta.Add("datecreated", Now().ToUniversalTime.ToString())
                        myMeta.Add("database", sDatabaseName)
                        Dim strCurrentDate As String = Now().ToUniversalTime().ToString("yy-MM-dd")
                        SendFile(strLocalStoragePath & rwTable.name & "-n.xml", strCurrentDate & "/" & rwTable.name & "-n.xml", "dbbackup", "text/xml", myMeta)
                        SendFile(strLocalStoragePath & rwTable.name & ".dat", strCurrentDate & "/" & rwTable.name & ".dat", "dbbackup", " text/plain", myMeta)
                        File.Delete(strLocalStoragePath & rwTable.name & "-n.xml")
                        File.Delete(strLocalStoragePath & rwTable.name & ".dat")
                    Catch ex As Exception
                        intTableErrorCount += 1
                        strOutput &= "Error backing up the " & strCurrentTable & ". " & ex.Message & "<br />"
                    End Try
                Next
            End With
            If intTableErrorCount > 0 Then
                strOutput = "There were " & intTableErrorCount.ToString() & " tables that had problems backing up.<br />" & strOutput
            End If
            strOutput = "Backed up " & intTableCount.ToString() & " tables.<br />" & strOutput
        Catch ex As Exception
            strOutput &= ex.Message & "<br />"
        End Try
        Return strOutput
    End Function

    Shared Function ExecuteBCP(ByVal sBCPSwitches As String) As Boolean
        Dim fileToRun As String = "bcp\bcp.exe"
        Dim appRoot As String = Environment.GetEnvironmentVariable("RoleRoot")
        Dim pathToFileToRun As String = Path.Combine(appRoot & "\", String.Format("approot\{0}", fileToRun))
        pathToFileToRun = pathToFileToRun
        Dim psi As New ProcessStartInfo(pathToFileToRun, sBCPSwitches)
        psi.CreateNoWindow = False
        psi.ErrorDialog = False
        psi.UseShellExecute = False
        psi.WindowStyle = ProcessWindowStyle.Hidden
        psi.RedirectStandardOutput = True
        psi.RedirectStandardError = True
        psi.UseShellExecute = False
        Try
            Dim exeProcess As New Process
            exeProcess.StartInfo = psi
            AddHandler exeProcess.OutputDataReceived, AddressOf NetOutputDataHandler
            netOutput = New StringBuilder()
            AddHandler exeProcess.ErrorDataReceived, AddressOf NetErrorDataHandler
            exeProcess.Start()
            exeProcess.BeginOutputReadLine()
            exeProcess.BeginErrorReadLine()
            exeProcess.WaitForExit()
            If Not streamError Is Nothing Then
                ' Close the error file.
                streamError.Close()
            Else
                ' Set errorsWritten to false if the stream is not
                ' open.   Either there are no errors, or the error
                ' file could not be opened.
                errorsWritten = False
            End If

            If netOutput.Length > 0 Then
                'Console.WriteLine()
                'Console.WriteLine("Public network shares from net view:")
                'Console.WriteLine()
                'Console.WriteLine(netOutput)
                'Console.WriteLine()
                Trace.WriteLine(netOutput)
            End If
            If errorsWritten Then
                ' Signal that the error file had something 
                ' written to it.
                'Dim errorOutput As String()
                'errorOutput = File.ReadAllLines(netErrorFile)
                'If errorOutput.Length > 0 Then

                '    Console.WriteLine(ControlChars.Lf + _
                '        "The following error output was appended to {0}.", _
                '        netErrorFile)
                '    Dim errLine As String
                '    For Each errLine In errorOutput
                '        Console.WriteLine("  {0}", errLine)
                '    Next

                '    Console.WriteLine()
                'End If
            End If
        Catch ex As Exception
            Trace.TraceError(ex.Message)
            Return False
        End Try
        Return True
    End Function

    Private Shared Sub timeDBBackup_Elapsed(ByVal source As Object, ByVal e As System.Timers.ElapsedEventArgs)
        'Console.WriteLine("The Elapsed event was raised at {0}", e.SignalTime)
        _timeDBBackup.Enabled = False
        BackupDB()
        _timeDBBackup.Enabled = True
    End Sub


    Private Shared Sub NetOutputDataHandler(ByVal sendingProcess As Object, ByVal outLine As DataReceivedEventArgs)
        Try
            ' Collect the net view command output.
            If Not String.IsNullOrEmpty(outLine.Data) Then
                ' Add the text to the collected output.
                netOutput.Append(Environment.NewLine + "  " + outLine.Data)
            End If
        Catch ex As Exception
            Trace.TraceError(ex.Message)
        End Try
    End Sub

    Private Shared Sub NetErrorDataHandler(ByVal sendingProcess As Object, ByVal errLine As DataReceivedEventArgs)
        Try
            ' Write the error text to the file if there is something to
            ' write and an error file has been specified.
            If Not String.IsNullOrEmpty(errLine.Data) Then
                If Not errorsWritten Then
                    If streamError Is Nothing Then
                        ' Open the file.
                        Try
                            'streamError = New StreamWriter(netErrorFile, True)
                        Catch e As Exception
                            Console.WriteLine("Could not open error file!")
                            Console.WriteLine(e.Message.ToString())
                        End Try
                    End If

                    If Not streamError Is Nothing Then
                        ' Write a header to the file if this is the first
                        ' call to the error output handler.
                        streamError.WriteLine()
                        streamError.WriteLine(DateTime.Now.ToString())
                        streamError.WriteLine("Net View error output:")
                    End If
                    errorsWritten = True
                End If

                If Not streamError Is Nothing Then
                    ' Write redirected errors to the file.
                    streamError.WriteLine(errLine.Data)
                    streamError.Flush()
                End If
            End If
        Catch ex As Exception
            Trace.TraceError(ex.Message)
        End Try
    End Sub

    Public Shared Function GetLocalDirectory(ByVal DirectoryName As String) As String
        Dim strDirectoryPath As String = DirectoryName
        Try
            If ServiceRuntime.RoleEnvironment.IsAvailable() Then
                Dim myStorage As Microsoft.WindowsAzure.ServiceRuntime.LocalResource = Microsoft.WindowsAzure.ServiceRuntime.RoleEnvironment.GetLocalResource("LocalStorage1") 'Defined in Role Properties LocalStorage
                If DirectoryName.Length > 0 And Not DirectoryName = "/" And Not DirectoryName = "\" Then
                    strDirectoryPath = System.IO.Path.Combine(myStorage.RootPath, DirectoryName)
                    If Not System.IO.Directory.Exists(strDirectoryPath) Then
                        System.IO.Directory.CreateDirectory(strDirectoryPath)
                    End If
                Else
                    strDirectoryPath = myStorage.RootPath
                End If
            End If
        Catch ex As Exception
            Trace.TraceError(ex.Message)
        End Try
        Return strDirectoryPath
    End Function

    Public Overloads Shared Function SendFile(ByVal FilePath As String, ByVal sFileName As String, ByVal Container As String, ByVal ContentType As String, ByVal FileMetaData As System.Collections.Specialized.NameValueCollection) As Boolean
        Dim BlobClient As CloudBlobClient = Nothing
        Dim BlobContainer As CloudBlobContainer = Nothing
        Try
            Dim storageAccount As CloudStorageAccount = CloudStorageAccount.Parse(ServiceRuntime.RoleEnvironment.GetConfigurationSettingValue("DataConnectionString")) 'Defined in Role Properties 
            BlobClient = storageAccount.CreateCloudBlobClient()
            ' Get and create the container
            BlobContainer = BlobClient.GetContainerReference(Container)
            BlobContainer.CreateIfNotExist()
            ' Setup the permissions on the container to be public
            Dim permissions = New BlobContainerPermissions()
            permissions.PublicAccess = BlobContainerPublicAccessType.Container
            BlobContainer.SetPermissions(permissions)
            ' Create the Blob and upload the file
            Dim blob = BlobContainer.GetBlobReference(sFileName)
            blob.UploadFile(FilePath)
            If FileMetaData.Count > 0 Then
                blob.Metadata.Add(FileMetaData)
            End If
            blob.SetMetadata()
            ' Set the properties
            blob.Properties.ContentType = ContentType
            blob.SetProperties()
            Return True
        Catch ex As Exception
            Trace.TraceError(ex.Message)
            Return False
        End Try
    End Function
End Class
