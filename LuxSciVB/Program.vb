Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks
Imports System.Net.Http.Headers
Imports System.Security.Cryptography
Imports System.Net
Imports Newtonsoft.Json
Imports System.IO
Imports Newtonsoft.Json.Bson

Namespace LuxSciWebApi
    Class Program
        Shared TOKEN As String = "XXXXXXXXXXXXXXXXXXXXXX"
        Shared SECRET As String = "XXXXXXXXXXXXXXXXXXXXX"
        Shared USER_TOKEN As String = "XXXXXXXXXXXXXXXXXXXXXX"
        Shared USER_SECRET As String = "XXXXXXXXXXXXXXXXXXXXXXXXX"
        Shared EMAIL_ID As String = "XXXX@XXXXXXX.luxsci.net"
        Private Shared EMAIL_PASSWORD As String = "XXXXX"
        Private Shared SUCCESS_RESPONSE As Integer = 1
        Private Shared ACCOUNT_CODE As String = "0000"
        Private Shared USER_PATH As String = "/perl/api/v2/user/"
        Private Shared ACCOUNT_PATH As String = "/perl/api/v2/account/"
        Private Shared AUTH_PATH As String = "perl/api/v2/auth"
        Private Shared REST_API_PATH As String = "https://rest.luxsci.com/"
        Private Shared ATTACHMENT_PATH As String = "E:\File2Mail.txt"  'make sure this file exists.

        Shared userAuth As String = ""
        Shared accountAuth As String = ""

        Public Shared Sub Main(ByVal args As String())
            Console.WriteLine("--------------------------------------------------------------------------------------------------")
            Console.WriteLine("----------------------------------Processing Requests under User Scope-----------------------------")

            'process all the requests under User Account Scope
            ProcessUserRequests().Wait()
            Console.WriteLine("----------------------------------Completed Requests under User Scope-------------------------------")
            Console.WriteLine("---------------------------------------------------------------------------------------------------")
            Console.WriteLine("----------------------------------Processing Requests under Account Scope--------------------------")

            'process all the requests under Account scope ( as admin)
            ProcessAccountRequests().Wait()
            Console.WriteLine("----------------------------------Completed Requests under Account Scope----------------------------")
            Console.ReadLine()
        End Sub

        Private Shared Async Function ProcessUserRequests() As Task
            Dim client As HttpClient = New HttpClient() 'set the http client

            client.BaseAddress = New Uri(REST_API_PATH)
            client.DefaultRequestHeaders.Accept.Clear()
            client.DefaultRequestHeaders.Accept.Add(New System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"))

            Dim ddate = ToUnixTimestamp(DateTime.UtcNow)  ' use unix formated date
            ' set the user signature
            Dim usersign = CreateToken((USER_TOKEN & vbLf & ddate & vbLf & EMAIL_ID & vbLf & EMAIL_PASSWORD & vbLf), USER_SECRET)

            'authentication request for the user
            Dim userRequest = New UserAuthRequest With {
                .user = EMAIL_ID,
                .pass = EMAIL_PASSWORD, ' password for the user
                .token = USER_TOKEN, 'use token obtained from the UI
                .sdate = ddate.ToString(),
                .signature = usersign  '// set the signature
            }

            ' authenticate the user And get the response
            Dim resp = Await AuthRequest(client, userRequest)

            ' see if the response is succcess
            If resp.success = SUCCESS_RESPONSE Then
                'display the output
                DisplayAuthOutput(resp)

                userAuth = resp.auth '// get and set the authentication token
                Dim method = "GET"
                Dim path = USER_PATH & EMAIL_ID & "/profile"
                Dim qs = "" 'query string
                Dim requestBodyHash = "" '// hash of the body

                ' sign the request
                Dim to_sign = CreateToken((userAuth & vbLf & method.ToUpper() & vbLf & path & vbLf & qs & vbLf & requestBodyHash & vbLf), USER_SECRET)

                ' get the response
                resp = Await GetDataAsync(GetNewHttpClient(userAuth, path, to_sign), path, Nothing)
                ProcessOutput(resp, "User Profile")


                'send mail api 
                Dim emailReq As EmailRequest = New EmailRequest()
                emailReq.Subject = "Hello, test message"
                emailReq.Body = "Dear User " & vbLf & " This is a test message for you from LuxSci, using amazing APIs. " & vbLf & " Regards " & vbLf & " LuxSciTeam"
                emailReq.[To] = New String() {"xxxxx@gmail.com", "xxxxx@yahoo.com", "yyyy@yyyy.com"}
                emailReq.FromName = "JohnDoeLuxSci"
                emailReq.FromAddress = EMAIL_ID
                emailReq.BodyType = "text"
                path = USER_PATH & EMAIL_ID & "/email/compose/secureline/send"
                method = "POST"
                ' sign the request
                to_sign = CreateToken((userAuth & vbLf & method.ToUpper() & vbLf & path & vbLf & qs & vbLf & ComputeSha256Hash(JsonConvert.SerializeObject(emailReq).Trim()) & vbLf), USER_SECRET)

                ' send and obtain the response from the send mail api call
                resp = Await PostDataAsync(GetNewHttpClient(userAuth, path, to_sign), path, JsonConvert.DeserializeObject(JsonConvert.SerializeObject(emailReq).Trim()))
                ProcessOutput(resp, "Send Mail")

                'Send Mail With Attachment and get the response
                Dim resp2 = Await SendMailWithAttachment()
                'display the response
                ProcessOutput(resp2, "Send Mail with attachment")

                'get the email report for the user
                method = "GET"
                path = USER_PATH & EMAIL_ID & "/report/email/webmail/sent"
                qs = ""
                requestBodyHash = ""
                to_sign = CreateToken((userAuth & vbLf & method.ToUpper() & vbLf & path & vbLf & qs & vbLf & requestBodyHash & vbLf), USER_SECRET)
                'get the email report response
                resp = Await GetDataAsync(GetNewHttpClient(userAuth, path, to_sign), path, Nothing)

                'display the email report response
                ProcessOutput(resp, "User Profile")
            Else
                'display failure of user auth web api call
                DisplayError(resp, "User Auth")
            End If
        End Function

        'process the requests in account scope
        Private Shared Async Function ProcessAccountRequests() As Task


            Dim client As HttpClient = New HttpClient() 'set the http client
            client.BaseAddress = New Uri(REST_API_PATH)
            client.DefaultRequestHeaders.Accept.Clear()
            client.DefaultRequestHeaders.Accept.Add(New System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"))
            Dim ddate = ToUnixTimestamp(DateTime.UtcNow) ' set date in unix format
            Dim sign = CreateToken((TOKEN & vbLf & ddate & vbLf), SECRET)

            'create the token for the request
            Dim acctsign = CreateToken((USER_TOKEN & vbLf & ddate & vbLf & EMAIL_ID & vbLf), SECRET)

            'authorization request
            Dim aReq = New AccountAuthRequest() With {
                .token = TOKEN, 'token obtained at the api
                .sdate = ddate.ToString(), ' date in unix format
                .signature = sign ' signature
            }

            'Authenticate and obtain the  authorization token
            Dim resp = Await AuthRequest(client, aReq)


            If resp.success = SUCCESS_RESPONSE Then
                'display the output
                DisplayAuthOutput(resp)

                accountAuth = resp.auth 'set authorization token
                Dim method = "GET"
                Dim path = ACCOUNT_PATH & ACCOUNT_CODE & "/users"
                Dim qs = ""
                Dim requestBodyHash = "" ' hash the body

                'token to sign the request
                Dim to_sign = CreateToken((accountAuth & vbLf & method.ToUpper() & vbLf & path & vbLf & qs & vbLf & requestBodyHash & vbLf), SECRET)


                'get users request
                resp = Await GetDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, Nothing)
                ProcessOutput(resp, "Account ")

                'create alias request
                Dim [alias] As AliasRequest = New AliasRequest()
                [alias].User = "JohnDoe"
                [alias].Action = "bounce" 'actions are bounce or email
                [alias].Domain = "demouser@xxxx.xxxx.luxsci.net"
                [alias].Dest = "demouser@xxxx.xxxx.luxsci.net"
                [alias].Bounce = "xxxxx@gmail.com"
                path = ACCOUNT_PATH & ACCOUNT_CODE & "/aliases"
                method = "POST"

                'token to sign the request, note that the JSON request is being hashed
                to_sign = CreateToken((accountAuth & vbLf & method.ToUpper() & vbLf & path & vbLf & qs & vbLf & ComputeSha256Hash(JsonConvert.SerializeObject([alias])) & vbLf), SECRET)

                'call the create alias request
                resp = Await PostDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, [alias])
                ProcessOutput(resp, "Create Alias ")

                'delete alias
                ' path = ACCOUNT_PATH & ACCOUNT_CODE & "/aliases/admin@apidev18.trial6.luxsci.net"
                'method = "DELETE"
                'to_sign = CreateToken((accountAuth & vbLf & method.ToUpper() & vbLf & path & vbLf & qs & vbLf & "" & vbLf), SECRET)
                'resp = Await DeleteDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, "")
                'ProcessOutput(resp, "Delete Alias ")

                'recieve email report sent
                method = "GET"
                path = ACCOUNT_PATH & ACCOUNT_CODE & "/report/email/smtp/sent"
                qs = "" 'query string
                requestBodyHash = "" 'hash the body request
                'sign the request
                to_sign = CreateToken((accountAuth & vbLf & method.ToUpper() & vbLf & path & vbLf & qs & vbLf & requestBodyHash & vbLf), SECRET)

                'recieve the mail report sent
                resp = Await GetDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, Nothing)
                'display the output
                ProcessOutput(resp, "Email Sent Report - SMTP ")

                'get the email sent  through webmail
                method = "GET"
                path = ACCOUNT_PATH & ACCOUNT_CODE & "/report/email/webmail/sent"
                qs = ""
                requestBodyHash = ""
                'sign the request
                to_sign = CreateToken((accountAuth & vbLf & method.ToUpper() & vbLf & path & vbLf & qs & vbLf & requestBodyHash & vbLf), SECRET)

                'recieve the response from get email report api call
                resp = Await GetDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, Nothing)
                ProcessOutput(resp, "Email Sent Report  Web Mail ")
            Else
                'display the error from user auth
                DisplayError(resp, "User Auth")
            End If
        End Function

        '  method to create http client, since we have to sign the request every time with the cookie and the Request Method could vary, lets use new client.
        Private Shared Function GetNewHttpClient(ByVal Auth As String, ByVal path As String, ByVal sign As String) As HttpClient

            'HttpClientHandler is a  variable
            Dim HttpClientHandler = New HttpClientHandler With {
                .AllowAutoRedirect = True,
                .UseCookies = True,
                .CookieContainer = New CookieContainer()
            }

            'create the client cookie with the signature
            Dim clientCookie As Cookie = New Cookie("signature", Auth & ":" & sign)
            clientCookie.Expires = DateTime.Now.AddDays(2)
            clientCookie.Domain = New Uri(REST_API_PATH).Host
            clientCookie.Path = "/"

            'set the httpClient handler with the cookie
            HttpClientHandler.CookieContainer.Add(clientCookie)
            Dim client As HttpClient = New HttpClient(HttpClientHandler, False)

            'set the client base address
            client.BaseAddress = New Uri(REST_API_PATH)
            client.DefaultRequestHeaders.Add("X-Version", "1")
            client.DefaultRequestHeaders.Accept.Clear()
            client.DefaultRequestHeaders.Accept.Add(New System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"))
            Return client
        End Function

        'method to process the output and display in the console
        Private Shared Sub ProcessOutput(ByVal resp As LuxSciResponse, ByVal method As String)
            If resp.success = SUCCESS_RESPONSE Then
                'display success and response obtained
                DisplayOutput(resp, method)
            Else
                'display failure
                DisplayError(resp, method)
            End If
        End Sub

        'method to Perform authentication request
        Private Shared Async Function AuthRequest(ByVal client As HttpClient, ByVal req As Object) As Task(Of LuxSciResponse)
            Return Await PostDataAsync(client, AUTH_PATH, req)
        End Function


        'displays the output in console
        Private Shared Sub DisplayOutput(ByVal resp As LuxSciResponse, ByVal method As String)
            Console.WriteLine(vbLf)
            Console.WriteLine(vbLf)
            Console.WriteLine("----------------------------------------------------------------------------")
            Console.WriteLine("------------------" & method & " Request was successfull--------------------")
            Console.WriteLine("Response:{0}" & vbLf & " Success {1}" & vbLf & " Data:{2}", resp, resp.success, If(resp.data Is Nothing, "", resp.data.ToString()))
            Console.WriteLine("------------------ " & method & " completed Successfully--------------------")
        End Sub

        'method to display the output of Authentication
        Private Shared Sub DisplayAuthOutput(ByVal resp As LuxSciResponse)
            Console.WriteLine(vbLf)
            Console.WriteLine(vbLf)
            Console.WriteLine("-----------------------------------------------------------------------------")
            Console.WriteLine("----------------------Auth request was successfull---------------------------")
            Console.WriteLine("Response:{0}" & vbLf & " Success {1}" & vbLf & " AUth:{2}", resp, resp.success, resp.auth)
            Console.WriteLine("----------------------Auth completed Successfully----------------------------")
        End Sub

        'method to display the error in console
        Private Shared Sub DisplayError(ByVal resp As LuxSciResponse, ByVal method As String)
            Console.WriteLine(vbLf)
            Console.WriteLine(vbLf)
            Console.WriteLine("------------------------------------------------------------------------------")
            Console.WriteLine("----------------------" & method & " Request failed  -------------------------")
            Console.WriteLine("{0}" & vbLf & "{1}" & vbLf & "{2}", resp, resp.success, resp.error_message)
            Console.WriteLine("--------------------" & method & " Request completed with Failure--------------")
        End Sub

        'create token based in SHA256 alogrithm
        Private Shared Function ComputeSha256Hash(ByVal rawData As String) As String

            'Create a SHA256   
            Using sha256Hash As SHA256 = SHA256.Create()
                'ComputeHash - returns byte array  
                Dim bytes As Byte() = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData))

                'Convert byte array to a string   
                Dim builder As StringBuilder = New StringBuilder()

                For i As Integer = 0 To bytes.Length - 1
                    builder.Append(bytes(i).ToString("x2"))
                Next

                Return builder.ToString()
            End Using
        End Function

        'method to post the data to LuxSci webapi and return the output
        Private Shared Async Function PostDataAsync(ByVal client As HttpClient, ByVal Path As String, ByVal o As Object, ByVal Optional isCompleteUrl As Boolean = False, ByVal Optional timeoutMinutes As Integer = -1) As Task(Of LuxSciResponse)
            Using client
                'set timeout for the request
                If timeoutMinutes > 0 Then
                    client.Timeout = New TimeSpan(0, timeoutMinutes, 0)
                End If

                Dim response = Await client.PostAsJsonAsync(Path, o)
                ' return the  LuxSciResponse resposne
                Return Await response.Content.ReadAsAsync(Of LuxSciResponse)()
            End Using
        End Function

        'method to get the data from LuxSci webapi and return the output
        Private Shared Async Function GetDataAsync(ByVal client As HttpClient, ByVal Path As String, ByVal o As Object, ByVal Optional isCompleteUrl As Boolean = False, ByVal Optional timeoutMinutes As Integer = -1) As Task(Of LuxSciResponse)
            Using client
                'set time out
                If timeoutMinutes > 0 Then
                    client.Timeout = New TimeSpan(0, timeoutMinutes, 0)
                End If

                Dim response = Await client.GetAsync(Path)
                'return the  LuxSciResponse resposne
                Return Await response.Content.ReadAsAsync(Of LuxSciResponse)()
            End Using
        End Function

        'method to delete the data from LuxSci webapi and return the output
        Private Shared Async Function DeleteDataAsync(ByVal client As HttpClient, ByVal Path As String, ByVal o As Object, ByVal Optional isCompleteUrl As Boolean = False, ByVal Optional timeoutMinutes As Integer = -1) As Task(Of LuxSciResponse)
            Using client
                'set the time out
                If timeoutMinutes > 0 Then
                    client.Timeout = New TimeSpan(0, timeoutMinutes, 0)
                End If

                Dim response = Await client.DeleteAsync(Path)
                'return the  LuxSciResponse resposne
                Return Await response.Content.ReadAsAsync(Of LuxSciResponse)()
            End Using
        End Function

        'convert date time to UNIX format (long)
        Public Shared Function ToUnixTimestamp(ByVal d As DateTime) As Long
            Dim epoch = d - New DateTime(1970, 1, 1, 0, 0, 0)
            Return CLng(epoch.TotalSeconds)
        End Function

        'create the token using SHA256
        Public Shared Function CreateToken(ByVal message As String, ByVal secret As String) As String
            'Create the token for submission
            Dim hmac = New HMACSHA256(Encoding.UTF8.GetBytes(secret))
            Dim hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message))
            Return BitConverter.ToString(hash).Replace("-", String.Empty)
        End Function

        'method to send the mail with the attachment
        Public Shared Async Function SendMailWithAttachment() As Task(Of LuxSciResponse)

            ' send mail


            'read the file using the fileInfo object
            Dim fi As FileInfo = New FileInfo(ATTACHMENT_PATH)
            Dim fileName As String = fi.Name
            Dim fileContents As Byte() = File.ReadAllBytes(fi.FullName)
            Dim fileStream As FileStream = File.OpenRead(ATTACHMENT_PATH)
            Dim filecontent As String

            'read the file contents
            Using sr = New StreamReader(fileStream)
                filecontent = sr.ReadToEnd()
            End Using

            'set the email attachment name and details in header
            Dim byteArrayContent As ByteArrayContent = New ByteArrayContent(fileContents)
            byteArrayContent.Headers.ContentDisposition = New ContentDispositionHeaderValue("form-data")
            byteArrayContent.Headers.ContentDisposition.Name = """files"""
            byteArrayContent.Headers.ContentDisposition.FileName = """" & Path.GetFileName(ATTACHMENT_PATH) & """"
            byteArrayContent.Headers.ContentType = New MediaTypeHeaderValue("application/octet-stream")
            Dim boundary As String = Guid.NewGuid().ToString()

            'set the email request
            Dim emailReq As EmailRequest = New EmailRequest()
            emailReq.Subject = "Hello, test message"
            emailReq.Body = "Dear User " & vbLf & " This is a test message for you from LuxSci, using amazing APIs. " & vbLf & " Regards " & vbLf & " LuxSciTeam"
            emailReq.[To] = New String() {"xxxxx@gmail.com"}
            emailReq.FromName = "JohnDoeLuxSci"
            emailReq.FromAddress = EMAIL_ID
            emailReq.BodyType = "text"

            'set the attachment detail
            Dim attachs As List(Of Attachment) = New List(Of Attachment)()
            Dim atch As Attachment = New Attachment()
            atch.Name = Path.GetFileName(ATTACHMENT_PATH)

            'hash the file contents
            atch.Hash = ComputeSha256Hash(filecontent)
            attachs.Add(atch)
            emailReq.Attachments = attachs.ToArray()
            Dim spath = USER_PATH & EMAIL_ID & "/email/compose/secureline/send"
            Dim method = "POST"
            Dim qs = ""  ' query string

            'set the mail header, boundary and content type
            Dim content = New MultipartFormDataContent(boundary)
            content.Headers.Remove("Content-Type")
            content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" & boundary)

            'add the file content in formdata header
            Dim byteArrayJson As ByteArrayContent = New ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(emailReq).Trim()))
            byteArrayContent.Headers.ContentDisposition = New ContentDispositionHeaderValue("form-data")
            byteArrayContent.Headers.ContentDisposition.Name = """files"""
            byteArrayContent.Headers.ContentDisposition.FileName = """" & Path.GetFileName(ATTACHMENT_PATH) & """"
            byteArrayContent.Headers.ContentType = New MediaTypeHeaderValue("application/octet-stream")

            'convert the Email request JSON into bytes with the LuxSci Standards and specifications.
            byteArrayJson.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json")
            byteArrayJson.Headers.ContentDisposition = New ContentDispositionHeaderValue("form-data")
            byteArrayJson.Headers.ContentDisposition.Name = """json"""
            byteArrayJson.Headers.ContentDisposition.FileName = """json.js"""

            'add both the Request and File attachment in  MultiPartDataContent as bytes
            content.Add(byteArrayJson)
            content.Add(byteArrayContent)

            'sign the contents with user secret
            Dim to_sign = CreateToken((userAuth & vbLf & method.ToUpper() & vbLf & spath & vbLf & qs & vbLf & ComputeSha256Hash(JsonConvert.SerializeObject(emailReq).Trim()) & vbLf), USER_SECRET)

            Dim HttpClientHandler = New HttpClientHandler With {
                .AllowAutoRedirect = True,
                .UseCookies = True,
                .CookieContainer = New CookieContainer()
            }
            'set the signature cookie
            Dim clientCookie As Cookie = New Cookie("signature", userAuth & ":" & to_sign)
            clientCookie.Expires = DateTime.Now.AddDays(2)
            clientCookie.Domain = New Uri(REST_API_PATH).Host
            clientCookie.Path = "/"
            'add the cookie to the handler
            HttpClientHandler.CookieContainer.Add(clientCookie)

            Dim httpClient As HttpClient = New HttpClient(HttpClientHandler, False)

            'set the base and request header
            httpClient.BaseAddress = New Uri(REST_API_PATH)
            httpClient.DefaultRequestHeaders.Add("X-Version", "1")
            httpClient.DefaultRequestHeaders.Accept.Clear()
            httpClient.DefaultRequestHeaders.Accept.Add(New System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("multipart/form-data"))
            httpClient.Timeout = TimeSpan.FromMilliseconds(300000)
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" & boundary)
            'set the httpRequestMessage, Please note that we cannot send the data directly through HttplClient as the multipartdocument header cannot be created properly.
            Dim request = New HttpRequestMessage(HttpMethod.Post, New Uri("https://rest.luxsci.com" & spath)) With {
                .Version = HttpVersion.Version10,
                .Content = content
            }

            Using httpClient
                'set the timeout
                httpClient.Timeout = New TimeSpan(0, 30000, 0)
                'send the request
                Dim response = Await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead)
                Return Await response.Content.ReadAsAsync(Of LuxSciResponse)()
            End Using
        End Function
    End Class

    ' request for Authenticating the user
    <JsonObject>
    Public MustInherit Class AuthRequest
        Public Property token As String
        Public Property secret As String
        Public Property debug As String
        <JsonProperty("date")>
        Public Property sdate As String
        Public Property signature As String
    End Class


    ' request to hold ALias Request
    <JsonObject>
    Partial Public Class AliasRequest
        <JsonProperty("user")>
        Public Property User As String
        <JsonProperty("domain")>
        Public Property Domain As String
        <JsonProperty("dest")>
        Public Property Dest As String
        <JsonProperty("action")>
        Public Property Action As String
        <JsonProperty("bounce")>
        Public Property Bounce As String
    End Class


    ' Account Authentication request
    <JsonObject>
    Public Class AccountAuthRequest
        Inherits AuthRequest
    End Class

    ' User Authentication request
    <JsonObject>
    Public Class UserAuthRequest
        Inherits AccountAuthRequest

        Public Property user As String
        Public Property pass As String
    End Class

    ' class to hold response
    <JsonObject>
    Public Class LuxSciResponse
        Public Property success As Integer
        Public Property error_message As String
        Public Property auth As String
        Public Property data As Object
    End Class

    'class to hold user profile
    <JsonObject>
    Public Class UserProfile
        <JsonProperty("last_access_date")>
        Public Property LastAccessDate As DateTime
        <JsonProperty("sms")>
        Public Property Sms As String
        <JsonProperty("disk_quota")>
        Public Property DiskQuota As Long
        <JsonProperty("status")>
        Public Property Status As String
        <JsonProperty("disk_usage")>
        Public Property DiskUsage As String
        <JsonProperty("contact")>
        Public Property Contact As String
        <JsonProperty("domainname")>
        Public Property Domainname As String
        <JsonProperty("custom3")>
        Public Property Custom3 As String
        <JsonProperty("state")>
        Public Property State As String
        <JsonProperty("over_quota")>
        Public Property OverQuota As Long
        <JsonProperty("city")>
        Public Property City As String
        <JsonProperty("fax")>
        Public Property Fax As String
        <JsonProperty("user")>
        Public Property User As String
        <JsonProperty("company")>
        Public Property Company As String
        <JsonProperty("country")>
        Public Property Country As String
        <JsonProperty("uid")>
        Public Property Uid As String
        <JsonProperty("flags")>
        Public Property Flags As String()
        <JsonProperty("email2")>
        Public Property Email2 As String
        <JsonProperty("services")>
        Public Property Services As String()
        <JsonProperty("email1")>
        Public Property Email1 As String
        <JsonProperty("street1")>
        Public Property Street1 As String
        <JsonProperty("custom1")>
        Public Property Custom1 As String
        <JsonProperty("modified")>
        Public Property Modified As DateTime
        <JsonProperty("created")>
        Public Property Created As DateTimeOffset
        <JsonProperty("zip")>
        Public Property Zip As String
        <JsonProperty("custom2")>
        Public Property Custom2 As String
        <JsonProperty("phone1")>
        Public Property Phone1 As String
        <JsonProperty("street2")>
        Public Property Street2 As String
        <JsonProperty("phone2")>
        Public Property Phone2 As String
    End Class


    ' class to hold Email Request
    <JsonObject>
    Partial Public Class EmailRequest
        <JsonProperty("attachments")>
        Public Property Attachments As Attachment()
        <JsonProperty("from_address")>
        Public Property FromAddress As String
        <JsonProperty("subject")>
        Public Property Subject As String
        <JsonProperty("no_tls_only")>
        Public Property NoTlsOnly As Long
        <JsonProperty("body")>
        Public Property Body As String
        <JsonProperty("to")>
        Public Property [To] As String()
        <JsonProperty("from_name")>
        Public Property FromName As String
        <JsonProperty("receipt")>
        Public Property Receipt As Long
        <JsonProperty("body_type")>
        Public Property BodyType As String
    End Class


    ' class to hold attachment
    <JsonObject>
    Partial Public Class Attachment
        <JsonProperty("hash")>
        Public Property Hash As String
        <JsonProperty("name")>
        Public Property Name As String
    End Class
End Namespace
