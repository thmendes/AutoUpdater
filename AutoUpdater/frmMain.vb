Imports System.IO
Imports System.IO.Compression
Imports System.Net
Imports System.Security.Cryptography
Imports System.Text.RegularExpressions

Public Class frmMain
    Dim gameServerIP = "" 'Add here
    Dim baseURL As String = "" 'Add here
    Dim versionFileURL As String = baseURL + "version.txt"
    Dim fileListURL As String = baseURL + "filelist.txt"
    Dim downloadFolderName As String = "$Patch$"
    Dim pakFolderName As String = "zips"
    Dim pakExtension As String = ".zip"
    Dim enableBrowser As Boolean = True
    Dim enableBrowserScrollbars As Boolean = False
    Dim browserURL As String = "" 'Add here (optional)
    Dim launcherLocalVersion As Integer = 0
    Dim launcherWebVersion As Integer = 0
    Dim myStartupPath As String
    Dim myFileName As String
    Dim progressBarCounter As Integer
    Dim progressBarCounterMax As Integer
    Dim progressText As String
    Dim updating = False
    'For debugging
    Dim disableSelfUpdates As Boolean = False

    Private Sub frmMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'Set transparent form background
        SetStyle(ControlStyles.SupportsTransparentBackColor, True)
        Me.BackColor = Color.Black
        Me.TransparencyKey = Color.Black

        'Get application startup path
        Dim myPath As String = Application.StartupPath
        myStartupPath = myPath + "\"
        myFileName = Path.GetFileName(Application.ExecutablePath)

        'Self-update check
        If myFileName.EndsWith("_") Then
            Threading.Thread.Sleep(2000) 'Try to delay 2 seconds
            Dim parentPath As String = Mid(myPath, 1, myPath.LastIndexOf("\"))
            Dim parentPathFileName As String = Mid(myFileName, 1, myFileName.Length - 1)
            File.Copy(myStartupPath + myFileName, parentPath + "\" + parentPathFileName, True)
            Shell(parentPath + "\" + parentPathFileName, vbNormalFocus)
            End
        End If

        'picRepair.Enabled = False
        'Exit Sub 'offline mode

        'Get launcher local version
        If File.Exists("version.ini") Then
            Using fileReader As StreamReader = New StreamReader("version.ini")
                launcherLocalVersion = Integer.Parse(fileReader.ReadLine)
            End Using
        End If

        'Get launcher web version
        Dim client As WebClient = New WebClient()
        Dim webReader As StreamReader = New StreamReader(client.OpenRead(versionFileURL))
        Dim iVersion As String = webReader.ReadLine
        If iVersion.Length > 0 Then
            launcherWebVersion = Integer.Parse(iVersion)
        End If
        webReader.Close()

        'Update if necessary
        If (launcherLocalVersion < launcherWebVersion) Then
            'Disable buttons
            picStart.Enabled = False
            picRepair.Enabled = False
            lblFileName.Text = ""
            UpdateTimer.Enabled = True
            Call New Action(AddressOf updateFromWeb).BeginInvoke(Nothing, Me)
        End If
    End Sub

    Private Function checkAddress(ByVal URL As String) As Boolean
        Dim req As WebRequest
        req = WebRequest.Create(URL)
        Dim resp As WebResponse
        Try
            resp = req.GetResponse()
            resp.Close()
            req = Nothing
            Return True
        Catch ex As Exception
            req = Nothing
            Return False
        End Try
    End Function

    Private Sub updateFromWeb()
        updating = True

        'Get info from web
        Dim myFileList As New List(Of String)
        Dim client As WebClient = New WebClient()
        Dim webReader As StreamReader = New StreamReader(client.OpenRead(fileListURL))
        While (webReader.Peek() <> -1)
            myFileList.Add(webReader.ReadLine())
        End While
        webReader.Close()

        'Initialize progrss bar
        'Set the progressbar maximum
        progressBarInit(myFileList.Count)

        'Check if download folder exists
        If (Directory.Exists(myStartupPath + downloadFolderName)) Then
            Directory.Delete(myStartupPath + downloadFolderName, True)
        End If
        Directory.CreateDirectory(myStartupPath + downloadFolderName)

        'Update files
        Dim fileHash As String
        Dim fileName As String
        For Each fileInfo In myFileList
            'Split info
            fileHash = fileInfo.Split(vbTab)(0)
            fileName = Regex.Replace(fileInfo.Split(vbTab)(1), "[^\u0020-\u007E]+", String.Empty) 'Use regex to remove non ASCII characters

            'Check for self update
            If fileName.ToLower.Equals(myFileName.ToLower) Then
                If disableSelfUpdates = False And (Not getFileHash(myStartupPath + fileName).Equals(fileHash)) Then
                    myFileName = fileName 'in case of case sensitivity
                    startSelfUpdate()
                    Exit Sub
                End If
                Continue For
            End If

            'Update progress bar
            'Threading.Thread.Sleep(100)
            progressBarInc()
            progressText = "Checking: " + fileName

            'Check if file exists
            If (File.Exists(myStartupPath + fileName)) Then
                'No need to update if hash is correct
                If getFileHash(myStartupPath + fileName).Equals(fileHash) Then
                    Continue For
                End If
            End If

            'Generate the download file name
            Dim downloadedFileName As String
            If fileName.Contains("\") Then
                downloadedFileName = myStartupPath + downloadFolderName + "\" + Mid(fileName, fileName.LastIndexOf("\") + 1, fileName.Length) + ".tmp"
            Else
                downloadedFileName = myStartupPath + downloadFolderName + "\" + fileName + ".tmp"
            End If

            'Change label to downloading
            'Threading.Thread.Sleep(100)
            progressText = "Downloading: " + fileName

            'Download the zip file
            Try
                client.DownloadFile(baseURL + pakFolderName + "/" + fileName.Replace("\", "/") + pakExtension, downloadedFileName)
            Catch ex As Exception
                'Could not be found on the server (network delay maybe)
                MsgBox("Problem downloading " + fileName, MsgBoxStyle.Critical)
                Continue For
            End Try

            'Extract the file
            If fileName.Contains("\") Then
                Dim fileFolderPath As String = myStartupPath + Mid(fileName, 1, fileName.LastIndexOf("\"))
                If (Not Directory.Exists(fileFolderPath)) Then
                    Directory.CreateDirectory(fileFolderPath)
                End If
            End If
            'Delete older file
            Dim fileExists As Boolean = False 'Used this boolean because File.Exists may lock the file
            If File.Exists(myStartupPath + fileName) Then
                fileExists = True
            End If
            If fileExists Then
                File.Delete(myStartupPath + fileName)
            End If

            ZipFile.ExtractToDirectory(downloadedFileName, myStartupPath)

            'Delete the downloaded file
            File.Delete(downloadedFileName)
        Next

        'Remove download folder
        Directory.Delete(myStartupPath + downloadFolderName, True)

        'Update version.ini
        Using outputFile As StreamWriter = New StreamWriter(myStartupPath + "version.ini", False, System.Text.Encoding.Unicode)
            outputFile.Write(launcherWebVersion.ToString)
            outputFile.Close()
        End Using

        'Finish message
        'Threading.Thread.Sleep(100)
        progressText = "Your files are up to date."

        updating = False
    End Sub

    Private Sub startSelfUpdate()
        'Generate the download file name
        Dim downloadedFileName = myStartupPath + downloadFolderName + "\" + myFileName + ".tmp"

        'Download the zip file
        Dim webClient As New WebClient()
        Try
            webClient.DownloadFile(baseURL + pakFolderName + "/" + myFileName + pakExtension, downloadedFileName)
        Catch ex As Exception
            'Could not be found on the server (network delay maybe)
            MsgBox("Problem downloading " + myFileName, MsgBoxStyle.Critical)
            Exit Sub
        End Try

        'Extract the file
        ZipFile.ExtractToDirectory(downloadedFileName, myStartupPath + downloadFolderName)

        'Rename the new launcher
        My.Computer.FileSystem.RenameFile(myStartupPath + downloadFolderName + "\" + myFileName, myFileName + "_")
        'Run the new launcher
        Shell(downloadFolderName + "\" + myFileName + "_", vbMinimizedNoFocus)
        'Close this launcher
        End
    End Sub

    Private Function getFileHash(ByVal fileName As String)
        Dim hash = SHA256.Create() 'Initializes a SHA-256 hash object
        Dim hashValue() As Byte

        'Read the file
        Dim fileStream As FileStream = File.OpenRead(fileName)
        fileStream.Position = 0
        hashValue = hash.ComputeHash(fileStream)
        fileStream.Close()

        'The array of bytes is converted into hexadecimal string
        Dim hashHex As String = ""
        Dim counter As Integer
        For counter = 0 To hashValue.Length - 1
            hashHex += hashValue(counter).ToString("X2") 'Convert each byte in hexadecimal
        Next counter

        'Hash is returned in lowercase
        Return hashHex.ToLower
    End Function

    'Make form dragable
    Dim drag As Boolean
    Dim mouseX As Integer
    Dim mouseY As Integer

    Private Sub frmMain_MouseDown(ByVal sender As Object, ByVal e As MouseEventArgs) Handles Me.MouseDown
        'If updating Then Exit Sub
        If e.Button = MouseButtons.Left Then
            drag = True
            mouseX = Cursor.Position.X - Me.Left
            mouseY = Cursor.Position.Y - Me.Top
        End If
    End Sub

    Private Sub frmMain_MouseMove(ByVal sender As Object, ByVal e As MouseEventArgs) Handles Me.MouseMove
        If drag Then
            Me.Top = Cursor.Position.Y - mouseY
            Me.Left = Cursor.Position.X - mouseX
        End If
    End Sub

    Private Sub frmMain_MouseUp(ByVal sender As Object, ByVal e As MouseEventArgs) Handles Me.MouseUp
        drag = False
    End Sub

    'Progress bar subs
    Private Sub progressBarInit(count As Integer)
        If Me.InvokeRequired Then
            'We are on the wrong thread, marshal the call to the UI thread
            Me.Invoke(Sub() progressBarInit(count))
        Else
            'Now we are on the UI thread, safe to update the control
            If count > 0 Then
                picLoadingFore.Width = 0
            End If
            progressBarCounter = 0
            progressBarCounterMax = count
        End If
    End Sub

    Private Sub progressBarInc()
        progressBarCounter = progressBarCounter + 1
    End Sub

    'Minimize from taskbar click
    Protected Overrides ReadOnly Property CreateParams() As CreateParams
        Get
            Dim CP As CreateParams = MyBase.CreateParams
            CP.Style = &HA0000
            Return CP
        End Get
    End Property

    'Button "Start"
    Private Sub picStart_Click(sender As Object, e As EventArgs) Handles picStart.Click
        If updating Then Exit Sub
        Try
            Shell("system_en\l2.exe", vbNormalFocus)
        Catch ex As Exception
            MsgBox("L2.bin not found! Try checking your files.", MsgBoxStyle.Critical, "Auto Updater")
            Exit Sub
        End Try
        End
    End Sub

    Private Sub picStart_MouseHover(sender As Object, e As EventArgs) Handles picStart.MouseHover
        picStart.Image = picStartOver.Image
    End Sub

    Private Sub picStart_MouseLeave(sender As Object, e As EventArgs) Handles picStart.MouseLeave
        picStart.Image = picStartNormal.Image
    End Sub

    Private Sub picStart_MouseDown(sender As Object, e As MouseEventArgs) Handles picStart.MouseDown
        picStart.Image = picStartSelected.Image
    End Sub

    'Button "File Repair"
    Private Sub picRepair_Click(sender As Object, e As EventArgs) Handles picRepair.Click
        If updating Then Exit Sub

        'Connectivity check
        If checkAddress(versionFileURL) Then
            'Disable buttons
            picStart.Enabled = False
            picRepair.Enabled = False
            lblFileName.Text = ""
            UpdateTimer.Enabled = True
            Call New Action(AddressOf updateFromWeb).BeginInvoke(Nothing, Me)
        Else
            MsgBox("Cannot reach the server!", MsgBoxStyle.Critical, "Auto Updater")
        End If
    End Sub

    Private Sub picRepair_MouseHover(sender As Object, e As EventArgs) Handles picRepair.MouseHover
        picRepair.Image = picRepairOver.Image
    End Sub

    Private Sub picRepair_MouseLeave(sender As Object, e As EventArgs) Handles picRepair.MouseLeave
        picRepair.Image = picRepairNormal.Image
    End Sub

    Private Sub picRepair_MouseDown(sender As Object, e As MouseEventArgs) Handles picRepair.MouseDown
        picRepair.Image = picRepairSelected.Image
    End Sub

    'On unexpected form close
    Private Sub frmMain_Closed(sender As Object, e As EventArgs) Handles Me.Closed
        End
    End Sub

    'Button "X" - Close
    Private Sub picClose_Click(sender As Object, e As EventArgs) Handles picClose.Click
        If updating Then
            Dim result As Integer = MessageBox.Show("Exit while updating?", "Warning", MessageBoxButtons.YesNo)
            If result = DialogResult.No Then
                Exit Sub
            End If
        End If
        End
    End Sub

    Private Sub picClose_MouseHover(sender As Object, e As EventArgs) Handles picClose.MouseHover
        picClose.Image = picCloseOver.Image
    End Sub

    Private Sub picClose_MouseLeave(sender As Object, e As EventArgs) Handles picClose.MouseLeave
        picClose.Image = picCloseNormal.Image
    End Sub

    Private Sub picClose_MouseDown(sender As Object, e As MouseEventArgs) Handles picClose.MouseDown
        picClose.Image = picCloseSelected.Image
    End Sub

    'Button "_" - Minimize
    Private Sub picMinimize_Click(sender As Object, e As EventArgs) Handles picMinimize.Click
        Me.WindowState = FormWindowState.Minimized
    End Sub

    Private Sub picMinimize_MouseHover(sender As Object, e As EventArgs) Handles picMinimize.MouseHover
        picMinimize.Image = picMinimizeOver.Image
    End Sub

    Private Sub picMinimize_MouseLeave(sender As Object, e As EventArgs) Handles picMinimize.MouseLeave
        picMinimize.Image = picMinimizeNormal.Image
    End Sub

    Private Sub picMinimize_MouseDown(sender As Object, e As MouseEventArgs) Handles picMinimize.MouseDown
        picMinimize.Image = picMinimizeSelected.Image
    End Sub

    Private Sub updateTimer_Tick(sender As Object, e As EventArgs) Handles UpdateTimer.Tick
        If Me.InvokeRequired Then
            'If we're not on the UI thread, re-invoke this method on the UI thread
            Me.Invoke(Sub() updateTimer_Tick(sender, e))
        Else
            'Now we are on the UI thread, safe to update the control
            If progressBarCounterMax > 0 AndAlso progressBarCounter > 0 Then ' Ensure progressBarCounter is not 0 to avoid division by zero
                Dim percentage As Double = progressBarCounter / CDbl(progressBarCounterMax) ' Ensure this is the correct calculation for your progress logic
                Dim newWidth As Integer = CInt(picLoadingBack.Width * percentage)
                picLoadingFore.Width = newWidth

                'Move picLoadingSeperator to the end of picLoadingFore (just for visuals), otherwise this is not needed
                picLoadingSeperator.Location = New Point(picLoadingFore.Location.X + newWidth, picLoadingFore.Location.Y)
            End If

            'Update info text
            lblFileName.Text = progressText

            'Re-enable buttons check
            If Not updating Then
                picRepair.Enabled = True
                picStart.Enabled = True
            End If
        End If
    End Sub
End Class
