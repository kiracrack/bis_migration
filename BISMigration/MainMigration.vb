Imports System
Imports System.Data
Imports System.Windows.Forms
Imports System.Data.SqlClient
Imports System.Data.OleDb
Imports System.ComponentModel
Imports MySql.Data.MySqlClient
Imports System.IO

Public Class MainMigration
    Public Worker As New System.ComponentModel.BackgroundWorker
    Delegate Sub ChangeTextsSafe(ByVal length As Long, ByVal val As Double, ByVal percent As Double, ByVal lbl As Label, ByVal pb As ProgressBar)
    Dim jobs As String
    Dim imgbox As PictureBox = New PictureBox
    Private Sub MainMigration_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If System.IO.File.Exists(file_conn) = False Then
            frmConnectionSetup.ShowDialog()
            End
        End If
        If OpenMysqlConnection() = True Then

        End If
        CheckStatus()
    End Sub

    Public Sub CheckStatus()
        If conn.State = ConnectionState.Open Then
            txtHost.Text = sqlserver
            txtStatus.Text = "CONNECTED"
            txtStatus.ForeColor = Color.Green
            cmdMigrate.Enabled = True
        Else
            txtHost.Text = sqlserver
            txtStatus.Text = "DISCONNECTED"
            txtStatus.ForeColor = Color.Red
            cmdMigrate.Enabled = False
        End If
    End Sub
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        Dim dialog As New OpenFileDialog()
        dialog.Filter = "Excel files |*.xls;*.xlsx"
        dialog.Title = "Please select excel file"
        'Encrypt the selected file. I'll do this later. :)
        If dialog.ShowDialog() = DialogResult.OK Then
            Dim dt As DataTable
            dt = ImportExceltoDatatable(dialog.FileName)
            TextBox1.Text = dialog.FileName
            MyDataGridView_Trace.DataSource = dt
            MyDataGridView_Trace.Visible = True
            txtTotalRows.Text = FormatNumber(MyDataGridView_Trace.RowCount, 0)
            CheckStatus()
        End If
    End Sub

    Public Shared Function ImportExceltoDatatable(filepath As String) As DataTable
        ' string sqlquery= "Select * From [SheetName$] Where YourCondition";
        Dim dt As New DataTable
        Try
            Dim ds As New DataSet()
            Dim constring As String = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" & filepath & ";Extended Properties=""Excel 12.0;HDR=YES;"""
            Dim con As New OleDbConnection(constring & "")
            con.Open()
            Dim myTableName = con.GetSchema("Tables").Rows(0)("TABLE_NAME")
            Dim sqlquery As String = String.Format("SELECT * FROM [{0}]", myTableName) ' "Select * From " & myTableName  
            Dim da As New OleDbDataAdapter(sqlquery, con)
            da.Fill(ds)
            dt = ds.Tables(0)
            Return dt
        Catch ex As Exception
            MsgBox(Err.Description, MsgBoxStyle.Critical)
            Return dt
        End Try
    End Function

    Private Sub MysqlServerSettingToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles MysqlServerSettingToolStripMenuItem.Click
        frmConnectionSetup.Show(Me)
    End Sub

    Private Sub cmdMigrate_Click(sender As Object, e As EventArgs) Handles cmdMigrate.Click
        cmdMigrate.Enabled = False
        If CheckBox1.Checked = True Then
            jobs = "uploading_data"
        Else
            jobs = "converting"
        End If

        StartJobSynch(jobs)
    End Sub

    Public Sub ResizedImage(ByVal img As PictureBox)
        If img.Image Is Nothing Then Exit Sub
        Dim Original As New Bitmap(img.Image)
        img.Image = Original

        Dim m As Int32 = 320
        Dim n As Int32 = m * Original.Height / Original.Width

        Dim Thumb As New Bitmap(m, n, Original.PixelFormat)
        Thumb.SetResolution(Original.HorizontalResolution, Original.VerticalResolution)

        Dim g As Graphics = Graphics.FromImage(Thumb)
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.High

        g.DrawImage(Original, New Rectangle(0, 0, m, n))
        img.Image = Thumb
    End Sub
    Public Sub StartJobSynch(ByVal job As String)
        Worker = New BackgroundWorker
        Worker.WorkerReportsProgress = True
        Worker.WorkerSupportsCancellation = True
        AddHandler Worker.DoWork, AddressOf DoWork
        AddHandler Worker.RunWorkerCompleted, AddressOf WorkerCompleted
        Worker.RunWorkerAsync(job)
    End Sub

    Private Sub DoWork(ByVal sender As System.Object, ByVal e As System.ComponentModel.DoWorkEventArgs)
        Dim WorkType As String = e.Argument
        If WorkType = "converting" Then ' CONVERTING FROM EXCEL TO MYSQL DATABASE
            pb1.Value = 0
            com.CommandText = "CREATE DATABASE IF NOT EXISTS migration;" : com.ExecuteNonQuery()
            com.CommandText = "DROP TABLE IF EXISTS `migration`.`tbldaddyroger`;" : com.ExecuteNonQuery()
            Dim columns As String = ""
            For Each col In MyDataGridView_Trace.Columns
                columns += "`" & col.Name & "` TEXT,"
            Next
            com.CommandText = "CREATE TABLE `migration`.`tbldaddyroger` (  `trnid` INTEGER UNSIGNED NOT NULL AUTO_INCREMENT, " & columns & " PRIMARY KEY (`trnid`)) ENGINE = InnoDB;" : com.ExecuteNonQuery()
            Dim safedelegate As New ChangeTextsSafe(AddressOf ChangeTexts)
            For i = 0 To MyDataGridView_Trace.RowCount - 1
                Dim RowData As String = ""
                For Each col In MyDataGridView_Trace.Columns
                    RowData += "`" & col.Name & "` = '" & rchar(MyDataGridView_Trace.Item(col.Name, i).Value().ToString) & "',"
                Next
                com.CommandText = "INSERT INTO  `migration`.`tbldaddyroger` set " & RowData.Remove(RowData.Length - 1, 1) : com.ExecuteNonQuery()
                Dim pr As Long = (i * 100) / MyDataGridView_Trace.RowCount
                Me.Invoke(safedelegate, MyDataGridView_Trace.RowCount, i, pr, lbl1, pb1)
            Next

        ElseIf WorkType = "uploading_data" Then
            Dim safedelegate As New ChangeTextsSafe(AddressOf ChangeTexts)
            pb2.Value = 0
            com.CommandText = "DELETE from " & sqldatabase & ".tblresidents where entryby='Migrated';" : com.ExecuteNonQuery()
            com.CommandText = "DELETE from filedir.tblactionquerylogs where performedby='Migrated';" : com.ExecuteNonQuery()
            com.CommandText = "DELETE from filedir.tblresidentsimage where residentid not in (select residentid from " & sqldatabase & ".tblresidents);" : com.ExecuteNonQuery()

            Dim dst = New DataSet
            Dim msda As New MySqlDataAdapter()
            If Val(CChar(txtStartRow.Text)) > 0 Then
                msda = New MySqlDataAdapter("select * from migration.tbldaddyroger limit " & Val(txtStartRow.Text) & "," & MyDataGridView_Trace.RowCount & " ", conn)
            Else
                msda = New MySqlDataAdapter("select * from migration.tbldaddyroger", conn)
            End If
            msda.Fill(dst, "tbldaddyroger")
            For i = 0 To dst.Tables(0).Rows.Count - 1
                With (dst.Tables(0))
                    Dim resid As String = getResidentidSequence()
                    com.CommandText = "INSERT INTO `tblresidents` set  " _
                           + " residentid='" & resid & "', " _
                           + " title='', " _
                           + " lastname='" & rchar(.Rows(i)("lname").ToString()) & "', " _
                           + " firstname='" & rchar(.Rows(i)("fname").ToString()) & "', " _
                           + " middlename='" & rchar(.Rows(i)("mname").ToString()) & "', " _
                           + " fullname='" & rchar(.Rows(i)("lname").ToString()) & ", " & rchar(.Rows(i)("fname").ToString()) & If(rchar(.Rows(i)("mname").ToString()) = "" Or rchar(.Rows(i)("mname").ToString()) = "_", "", ", " & rchar(.Rows(i)("mname").ToString())) & "', " _
                           + " extname='', " _
                           + " nickname='" & rchar(.Rows(i)("Nickname").ToString()) & "', " _
                           + If(.Rows(i)("Birthdate").ToString() = "", "", " birthdate='" & ConvertDate(.Rows(i)("Birthdate").ToString()) & "', ") _
                           + " birthplace='', " _
                           + " gender='" & .Rows(i)("Gender").ToString() & "', " _
                           + " civilstatus='" & .Rows(i)("status").ToString() & "', " _
                           + " nationality='', " _
                           + " religion='" & .Rows(i)("Religion").ToString() & "', " _
                           + " bloodtype='" & .Rows(i)("Bloodtype").ToString() & "', " _
                           + " bodytype='', " _
                           + " healthissue='', " _
                           + " profession='" & .Rows(i)("occupation").ToString() & "', " _
                           + " skills='', " _
                           + " incomebracket='', " _
                           + " height='', " _
                           + " weight='', " _
                           + " nationalid='', " _
                           + " driverlicense='', " _
                           + " voters='" & If(.Rows(i)("registeredvoter").ToString().Contains("YES") = True, 1, 0) & "', " _
                           + " votersid='', " _
                           + " precinctno='" & .Rows(i)("Precinctno").ToString() & "', " _
                           + " colorcode='', " _
                           + " livingwithparents='0', " _
                           + " seniorcitizenmember='0', " _
                           + " pwd='0', " _
                           + " singleparent='0', " _
                           + " indigent='0', " _
                           + " tin='', " _
                           + " sss='', " _
                           + " pagibig='', " _
                           + " philhealth='', " _
                           + " region='', " _
                           + " province='" & GlobalProvinceCode & "', " _
                           + " municipality='" & GlobalMunicipalityCode & "', " _
                           + " barangay='" & GlobalBarangayCode & "', " _
                           + " permanentresident='1', " _
                           + " purok='', " _
                           + " residencetype='', " _
                           + " notpermanentresidentaddress='', " _
                           + " dateresidential=null, " _
                           + " streetaddress='" & rchar(.Rows(i)("spousefname1").ToString()) & "', " _
                           + " celnumber='" & If(.Rows(i)("telnum").ToString().Length = 10, "0" & .Rows(i)("telnum").ToString(), "") & "', " _
                           + " homenumber='" & If(.Rows(i)("telnum").ToString().Length < 10, .Rows(i)("telnum").ToString(), "") & "', " _
                           + " emailaddress='" & .Rows(i)("email").ToString() & "', " _
                           + " facebookurl='" & .Rows(i)("website").ToString() & "', " _
                           + " deceased='0', " _
                           + " datedeceased=null, " _
                           + " causeofdeath='', " _
                           + " sector='', " _
                           + " dateentry=current_timestamp, " _
                           + " entryby='Migrated', " _
                           + " isedited='0', " _
                           + " editedby='', " _
                           + " dateedited=null, " _
                           + " incompleteinfo=0;" : com.ExecuteNonQuery()
                    LogQuery(Me.Text, com.CommandText.ToString, Me.Text)

                    If ckPicture.Checked = True Then
                        For Each LogFile In Directory.GetFiles(txtDir.Text)
                            If Path.GetFileName(LogFile).Contains(.Rows(i)("id").ToString()) = True Then
                                imgbox.Image = Bitmap.FromFile(LogFile)
                                ResizedImage(imgbox)
                                com.CommandText = "insert into filedir.tblresidentsimage set residentid='" & resid & "'" : com.ExecuteNonQuery()
                                LogQuery(Me.Text, com.CommandText.ToString, Me.Text)

                                UpdateImage("residentid='" & resid & "'", "imgprofile", "filedir.tblresidentsimage", imgbox)
                                Exit For
                            End If
                        Next
                    End If

                End With
                Dim pr As Long = (i * 100) / dst.Tables(0).Rows.Count
                Me.Invoke(safedelegate, dst.Tables(0).Rows.Count, i, pr, lbl2, pb2)
            Next
        End If

    End Sub

    Private Sub WorkerCompleted(ByVal sender As System.Object, ByVal e As System.ComponentModel.RunWorkerCompletedEventArgs)
        If jobs = "converting" Then
            Worker.Dispose()
            jobs = "uploading_data"
            StartJobSynch(jobs)
        Else
            cmdMigrate.Enabled = True
            MsgBox("Database migration successfully done! ", MsgBoxStyle.Information)
        End If
    End Sub

    Public Sub ChangeTexts(ByVal length As Long, ByVal val As Integer, ByVal percent As Double, ByVal lbl As Label, ByVal pb As ProgressBar)
        If jobs = "converting" Then
            lbl.Text = "Converting from Excel to Database.. " & FormatNumber(val, 0) & " of " & FormatNumber(length, 0) & " Rows (" & pb.Value & "%)"

        ElseIf jobs = "uploading_data" Then
            lbl.Text = "Uploading Image to Database.. " & FormatNumber(val, 0) & " of " & FormatNumber(length, 0) & " Rows (" & pb.Value & "%)"

        End If
        'MyDataGridView_Trace.Rows(val).Selected = True
        'MyDataGridView_Trace.FirstDisplayedScrollingRowIndex = val
        'MyDataGridView_Trace.Update()
        pb.Value = percent
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim fd As New FolderBrowserDialog
        If (fd.ShowDialog() = DialogResult.OK) Then
            txtDir.Text = fd.SelectedPath
        End If
    End Sub

    Private Sub MonitoringToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles MonitoringToolStripMenuItem.Click
        MainMonitoring.Show()
    End Sub
End Class
