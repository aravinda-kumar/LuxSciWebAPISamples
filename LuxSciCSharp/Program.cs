using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Net;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Bson;

namespace LuxSciWebApi
{
    class Program
    {
        static string TOKEN = "xxxxxxxxxxxxx";
        static string SECRET = "xxxxxxxxxxxx";
        static string USER_TOKEN = "xxxxxxxxxx";
        static string USER_SECRET = "xxxxxxxxxxxxxx";
        static string EMAIL_ID = "admin@xxxxxxx.luxsci.net";
        private static string EMAIL_PASSWORD = "xxx!x";

        private static int SUCCESS_RESPONSE = 1;
        private static string ACCOUNT_CODE = "18184";
        private static string USER_PATH = "/perl/api/v2/user/";
        private static string ACCOUNT_PATH = "/perl/api/v2/account/";
        private static string AUTH_PATH = "perl/api/v2/auth";
        private static string REST_API_PATH = "https://rest.luxsci.com/";
        private static string ATTACHMENT_PATH = "E:\\File2Mail.txt"; // make sure this file exists.

        static string userAuth = "";
        static string accountAuth = "";
        static void Main(string[] args)
        {


            // process all the requests under User Account Scope
            Console.WriteLine("--------------------------------------------------------------------------------------------------");
            Console.WriteLine("----------------------------------Processing Requests under User Scope-----------------------------");
            ProcessUserRequests().Wait();
            Console.WriteLine("----------------------------------Completed Requests under User Scope-------------------------------");
            // process all the requests under Account scope ( as admin)
            Console.WriteLine("---------------------------------------------------------------------------------------------------");
            Console.WriteLine("----------------------------------Processing Requests under Account Scope--------------------------");
            ProcessAccountRequests().Wait();
            Console.WriteLine("----------------------------------Completed Requests under Account Scope----------------------------");
            Console.ReadLine();

        }

        static async Task ProcessUserRequests()
        {
            //set the http client
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(REST_API_PATH);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));


            var date = ToUnixTimestamp(DateTime.UtcNow);// use unix formated date
            // set the user sign
            var usersign = CreateToken((USER_TOKEN + "\n" + date + "\n" + EMAIL_ID + "\n" + EMAIL_PASSWORD + "\n"), USER_SECRET);

            //authentication request for the user
            var userRequest = new UserAuthRequest
            {
                user = EMAIL_ID,
                pass = EMAIL_PASSWORD, // password for the user
                token = USER_TOKEN, // use token obtained from the UI
                date = date.ToString(),
                signature = usersign // set the signature
            };
            // authenticate the user and get the response
            var resp = await AuthRequest(client, userRequest);

            if (resp.success == SUCCESS_RESPONSE)
            {
                DisplayAuthOutput(resp);// display the output

                userAuth = resp.auth; // get the authentication token

                var method = "GET";
                var path = USER_PATH + EMAIL_ID + "/profile";
                var qs = ""; // query string
                var requestBodyHash = "";// hash of the body
                var to_sign =  // sign the create
                    CreateToken(
                        (userAuth + "\n" + method.ToUpper() + "\n" + path + "\n" + qs + "\n" + requestBodyHash + "\n"),
                        USER_SECRET);


                //get the response
                resp = await GetDataAsync(GetNewHttpClient(userAuth, path, to_sign), path, null);
                ProcessOutput(resp, "User Profile"); // display the output



                // send mail
                
                EmailRequest emailReq = new EmailRequest();

                emailReq.Subject = "Hello, test message";
                emailReq.Body =
                    "Dear User \n This is a test message for you from LuxSci, using amazing APIs. \n Regards \n LuxSciTeam";
                emailReq.To = new string[] {"xxx@gmail.com", "yyyy@yahoo.com", "zzzz@zzzz.com"};
                emailReq.FromName = "Bruce Wayne";
                emailReq.FromAddress = EMAIL_ID;
                emailReq.BodyType = "text";
                path = USER_PATH + EMAIL_ID + "/email/compose/secureline/send";
                method = "POST";
                to_sign = CreateToken(
                    (userAuth + "\n" + method.ToUpper() + "\n" + path + "\n" + qs + "\n" +
                     ComputeSha256Hash(JsonConvert.SerializeObject(emailReq).Trim()) + "\n"), USER_SECRET);
                resp = await PostDataAsync(GetNewHttpClient(userAuth, path, to_sign), path,
                    JsonConvert.DeserializeObject(JsonConvert.SerializeObject(emailReq).Trim()));
                ProcessOutput(resp, "Send Mail");
    
               // Send Mail With Attachment
                var resp2 = await SendMailWithAttachment();
                ProcessOutput(resp2, "Send Mail with attachment"); // display output



                // get the email report for the user
              
                 method = "GET";
                 path = USER_PATH + EMAIL_ID + "/report/email/webmail/sent";
                 qs = ""; // query string
                 requestBodyHash = "";// hash of the body
                 to_sign =  // sign the create
                    CreateToken(
                        (userAuth + "\n" + method.ToUpper() + "\n" + path + "\n" + qs + "\n" + requestBodyHash + "\n"),
                        USER_SECRET);


                //get the response for email report
                resp = await GetDataAsync(GetNewHttpClient(userAuth, path, to_sign), path, null);
                ProcessOutput(resp, "User Profile"); // display the output


            }
            else
            {
                // handle failure
                DisplayError(resp, "User Auth");

            }

        }

        static async Task ProcessAccountRequests()
        {

            // process the requests in account scope
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(REST_API_PATH);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")); // json request type header



            var date = ToUnixTimestamp(DateTime.UtcNow); // set date in unix format
            var sign = CreateToken((TOKEN + "\n" + date + "\n"), SECRET); // sign the token
            var acctsign = CreateToken((USER_TOKEN + "\n" + date + "\n" + EMAIL_ID + "\n"), SECRET); // sign the request with the secret obtained while setting the API


            // authorization request
            var aReq = new AccountAuthRequest()
            {
                token = TOKEN, // token obtained at the api
                date = date.ToString(), // date in unix format
                signature = sign   // signature
            };

            // Authenticate and obtain the  authorization token
            var resp = await AuthRequest(client, aReq);

            if (resp.success == SUCCESS_RESPONSE)
            {
                DisplayAuthOutput(resp);// display the output
                accountAuth = resp.auth; //set authorization token

                var method = "GET";
                var path = ACCOUNT_PATH + ACCOUNT_CODE + "/users"; // url
                var qs = ""; // query string
                var requestBodyHash = ""; // hash the body
                var to_sign =   // token to sign the request
                    CreateToken(
                        (accountAuth + "\n" + method.ToUpper() + "\n" + path + "\n" + qs + "\n" + requestBodyHash +
                         "\n"), SECRET);



                //get profile request
                resp = await GetDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, null);
                ProcessOutput(resp, "Account "); // display the output of the profile request


                // Create alias Request
                AliasRequest alias = new AliasRequest();
                alias.User = "JohnDoe";
                alias.Action = "bounce"; // actions are bounce or email
                alias.Domain = "demouser@test.trial6.luxsci.net";
                alias.Dest = "demouser@test.trial6.luxsci.net";
                alias.Bounce = "xxxx@gmail.com";
                path = ACCOUNT_PATH + ACCOUNT_CODE + "/aliases";
                method = "POST";
                // token to sign the request, note that the JSON request is being hashed
                to_sign = CreateToken(
                    (accountAuth + "\n" + method.ToUpper() + "\n" + path + "\n" + qs + "\n" +
                     ComputeSha256Hash(JsonConvert.SerializeObject(alias)) + "\n"), SECRET);
                resp = await PostDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, alias);
                ProcessOutput(resp, "Create Alias ");


                // Delete Alias Request
                /**
                path = ACCOUNT_PATH + ACCOUNT_CODE + "/aliases/admin@xxxxx.xxx.luxsci.net";
                method = "DELETE";
                to_sign = CreateToken(
                    (accountAuth + "\n" + method.ToUpper() + "\n" + path + "\n" + qs + "\n" + "" + "\n"), SECRET);
                resp = await DeleteDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, "");
                ProcessOutput(resp, "Delete Alias ");
                */

                // get the email sent report
                 method = "GET";
                 path = ACCOUNT_PATH + ACCOUNT_CODE + "/report/email/smtp/sent"; // url
                 qs = ""; // query string
                 requestBodyHash = ""; // hash the body
                  to_sign =   // token to sign the request
                    CreateToken(
                        (accountAuth + "\n" + method.ToUpper() + "\n" + path + "\n" + qs + "\n" + requestBodyHash +
                         "\n"), SECRET);

                // get email sent request
                resp = await GetDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, null);
                ProcessOutput(resp, "Email Sent Report - SMTP "); // display the output of the profile request



                // get the email sent  through webmail
                method = "GET";
                path = ACCOUNT_PATH + ACCOUNT_CODE + "/report/email/webmail/sent"; // url
                qs = ""; // query string
                requestBodyHash = ""; // hash the body
                to_sign =   // token to sign the request
                    CreateToken(
                        (accountAuth + "\n" + method.ToUpper() + "\n" + path + "\n" + qs + "\n" + requestBodyHash +
                         "\n"), SECRET);

                // get email sent request
                resp = await GetDataAsync(GetNewHttpClient(accountAuth, path, to_sign), path, null);
                ProcessOutput(resp, "Email Sent Report  Web Mail "); // display the output of the profile request

            }
            else
            {
                // handle failure
                DisplayError(resp, "User Auth");

            }

        }
        // method to create http client, since we have to sign the request every time with the cookie and the Request Method could vary, lets use new client.
        private static HttpClient GetNewHttpClient(string Auth, string path, string sign)
        {
            //HttpClientHandler is a global variable                
            var HttpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };
            // create the client cookie with the signature
            Cookie clientCookie = new Cookie("signature", Auth + ":" + sign);
            clientCookie.Expires = DateTime.Now.AddDays(2);
            clientCookie.Domain = new Uri(REST_API_PATH).Host;
            clientCookie.Path = "/";

            // set the httpClient handler with the cookie
            HttpClientHandler.CookieContainer.Add(clientCookie);
            HttpClient client = new HttpClient(HttpClientHandler, false);

            client.BaseAddress = new Uri(REST_API_PATH);
            client.DefaultRequestHeaders.Add("X-Version", "1");

            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")); // set the request type

            return client;
        }


        // method to process the output and display in the console
        static void ProcessOutput(LuxSciResponse resp, string method)
        {
            if (resp.success == SUCCESS_RESPONSE)
            {
                DisplayOutput(resp, method);
            }
            else
            {
                // handle failure
                DisplayError(resp, method);

            }
        }
        // method to Perform authentication request
        static async Task<LuxSciResponse> AuthRequest(HttpClient client, object req)
        {
            return await PostDataAsync(client, AUTH_PATH, req);

        }

        // displays the output in console
        static void DisplayOutput(LuxSciResponse resp, string method)
        {
            Console.WriteLine("\n");
            Console.WriteLine("\n");
            Console.WriteLine("----------------------------------------------------------------------------");
            Console.WriteLine("------------------" + method + " Request was successfull--------------------");
            Console.WriteLine("Response:{0}\n Success {1}\n Data:{2}", resp, resp.success,
                resp.data == null ? "" : resp.data.ToString());
            Console.WriteLine("------------------ " + method + " completed Successfully--------------------");

        }
        // method to display the output of Authentication
        static void DisplayAuthOutput(LuxSciResponse resp)
        {
            Console.WriteLine("\n");
            Console.WriteLine("\n");
            Console.WriteLine("-----------------------------------------------------------------------------");
            Console.WriteLine("----------------------Auth request was successfull---------------------------");
            Console.WriteLine("Response:{0}\n Success {1}\n AUth:{2}", resp, resp.success, resp.auth);
            Console.WriteLine("----------------------Auth completed Successfully----------------------------");

        }
        // method to display the error in console
        static void DisplayError(LuxSciResponse resp, string method)
        {
            Console.WriteLine("\n");
            Console.WriteLine("\n");
            Console.WriteLine("------------------------------------------------------------------------------");
            Console.WriteLine("----------------------" + method + " Request failed  -------------------------");
            Console.WriteLine("{0}\n{1}\n{2}", resp, resp.success, resp.error_message);
            Console.WriteLine("--------------------" + method + " Request completed with Failure--------------");
            
        }

       // create token based in SHA256 alogrithm
        static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        // method to post the data to LuxSci webapi and return the output
        static async Task<LuxSciResponse> PostDataAsync(HttpClient client, string Path, Object o,
            Boolean isCompleteUrl = false, int timeoutMinutes = -1)
        {
            using (client)
            {
                if (timeoutMinutes > 0)
                {
                    client.Timeout = new TimeSpan(0, timeoutMinutes, 0); //set timeout for the request
                }

                var response = await client.PostAsJsonAsync(Path, o);
                return await response.Content.ReadAsAsync<LuxSciResponse>();// return the  LuxSciResponse resposne
            }
        }

        // method to get the data from LuxSci webapi and return the output
        static async Task<LuxSciResponse> GetDataAsync(HttpClient client, string Path, Object o,
            Boolean isCompleteUrl = false, int timeoutMinutes = -1)
        {
            using (client)
            {
                if (timeoutMinutes > 0)
                {
                    client.Timeout = new TimeSpan(0, timeoutMinutes, 0);// set time out
                }

                var response = await client.GetAsync(Path);
                return await response.Content.ReadAsAsync<LuxSciResponse>();// return the  LuxSciResponse resposne
            }
        }

        // method to delete the data from LuxSci webapi and return the output
        static async Task<LuxSciResponse> DeleteDataAsync(HttpClient client, string Path, Object o,
            Boolean isCompleteUrl = false, int timeoutMinutes = -1)
        {
            using (client)
            {
                if (timeoutMinutes > 0)
                {
                    client.Timeout = new TimeSpan(0, timeoutMinutes, 0);// set the time out
                }

                var response = await client.DeleteAsync(Path);
                return await response.Content.ReadAsAsync<LuxSciResponse>();// return the  LuxSciResponse resposne
            }
        }

        // convert date time to UNIX format (long)
        public static long ToUnixTimestamp(DateTime d)
        {
            var epoch = d - new DateTime(1970, 1, 1, 0, 0, 0);
            return (long) epoch.TotalSeconds;
        }

        // create the token using SHA256
        public static string CreateToken(string message, string secret)
        {
            //Create the token for submission
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        // method to send the mail with the attachment
        public static async Task<LuxSciResponse> SendMailWithAttachment()
        {


            // send mail
            

            // read the file using the fileInfo object
            FileInfo fi = new FileInfo(ATTACHMENT_PATH);
            string fileName = fi.Name;
            byte[] fileContents = File.ReadAllBytes(fi.FullName);


            //read the file contents
            FileStream fileStream = File.OpenRead(ATTACHMENT_PATH);
           // var streamContent = new StreamContent(fileStream);

            string filecontents;
            // set the email attachment name and details in header
            using (var sr = new StreamReader(fileStream))
            {
                filecontents = sr.ReadToEnd();
            }
            ByteArrayContent byteArrayContent = new ByteArrayContent(fileContents);
            byteArrayContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data");
            byteArrayContent.Headers.ContentDisposition.Name = "\"files\"";
            byteArrayContent.Headers.ContentDisposition.FileName = "\"" + Path.GetFileName(ATTACHMENT_PATH) + "\"";
            byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            string boundary = Guid.NewGuid().ToString();



            // set the email request
            EmailRequest emailReq = new EmailRequest();

            emailReq.Subject = "Hello, test message";
            emailReq.Body =
                "Dear User \n This is a test message for you from LuxSci, using amazing APIs. \n Regards \n LuxSciTeam";
            emailReq.To = new string[] {"xxxxx@gmail.com"};
            emailReq.FromName = "Bruce Wayne";
            emailReq.FromAddress = EMAIL_ID;
            emailReq.BodyType = "text";


            //set the attachment detail
            List<Attachment> attachs = new List<Attachment>();
            Attachment atch = new Attachment();
            atch.Name = Path.GetFileName(ATTACHMENT_PATH);
            atch.Hash = ComputeSha256Hash(filecontents);// hash the file contents
            attachs.Add(atch);
            emailReq.Attachments = attachs.ToArray();

            var path = USER_PATH + EMAIL_ID + "/email/compose/secureline/send";
            var method = "POST";
            var qs = "";// query string
            
            // set the mail header, boundary and content type
            var content = new MultipartFormDataContent(boundary);
            content.Headers.Remove("Content-Type");
             content.Headers.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary=" + boundary);


            // add the file content in formdata header
            ByteArrayContent byteArrayJson = new ByteArrayContent(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(emailReq).Trim()));
            byteArrayContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data");
            byteArrayContent.Headers.ContentDisposition.Name = "\"files\"";
            byteArrayContent.Headers.ContentDisposition.FileName = "\"" + Path.GetFileName(ATTACHMENT_PATH) + "\"";
            byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");


            //convert the Email request JSON into bytes with the LuxSci Standards and specifications.
            byteArrayJson.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            byteArrayJson.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data");
            byteArrayJson.Headers.ContentDisposition.Name = "\"json\"";
            byteArrayJson.Headers.ContentDisposition.FileName = "\"json.js\"";


            ///add both the Request and File attachment in  MultiPartDataContent as bytes
            content.Add(byteArrayJson);
            content.Add(byteArrayContent);

           //sign the contents with user secret
            var to_sign =
                CreateToken(
                    (userAuth + "\n" + method.ToUpper() + "\n" + path + "\n" + qs + "\n" +
                     ComputeSha256Hash(JsonConvert.SerializeObject(emailReq).Trim()) + "\n"), USER_SECRET);



            var HttpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                CookieContainer = new CookieContainer()
            };
             // set the signature cookie
            Cookie clientCookie = new Cookie("signature", userAuth + ":" + to_sign);
            clientCookie.Expires = DateTime.Now.AddDays(2);
            clientCookie.Domain = new Uri(REST_API_PATH).Host;
            clientCookie.Path = "/";

            //add the cookie to the handler
            HttpClientHandler.CookieContainer.Add(clientCookie);
            HttpClient httpClient = new HttpClient(HttpClientHandler, false);
            //set the base and request header
            httpClient.BaseAddress = new Uri(REST_API_PATH);
            httpClient.DefaultRequestHeaders.Add("X-Version", "1");
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("multipart/form-data"));
            httpClient.Timeout = TimeSpan.FromMilliseconds(300000);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "multipart/form-data; boundary="+boundary);

             //set the httpRequestMessage, Please note that we cannot send the data directly through HttplClient as the multipartdocument header cannot be created properly.
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri("https://rest.luxsci.com" + path))
            {
                Version = HttpVersion.Version10,
                Content = content
            };


            using (httpClient)
            {

                httpClient.Timeout = new TimeSpan(0, 30000, 0);
                //send the request
                var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead);
                    return await response.Content.ReadAsAsync<LuxSciResponse>();//recieve the response
            }

        }
    }


    // requeest for Authenticating the user
    [JsonObject]
    public abstract class AuthRequest
    {
        public string token { get; set; }
        public string secret { get; set; }
        public string debug { get; set; }
      
        public string date { get; set; }
        public string signature { get; set; }
    }

    // request to hold ALias Request
    [JsonObject]
    public partial class AliasRequest
    {
        [JsonProperty("user")] public string User { get; set; }

        [JsonProperty("domain")] public string Domain { get; set; }

        [JsonProperty("dest")] public string Dest { get; set; }

        [JsonProperty("action")] public string Action { get; set; }

        [JsonProperty("bounce")] public string Bounce { get; set; }
    }

    //Account Authentication request
    [JsonObject]
    public class AccountAuthRequest : AuthRequest
    {



    }

    //User Authentication request
    [JsonObject]
    public class UserAuthRequest : AccountAuthRequest
    {

        public string user { get; set; }
        public string pass { get; set; }

    }

    // class to hold response
    [JsonObject]
    public class LuxSciResponse
    {
        public int success { get; set; }
        public string error_message { get; set; }
        public string auth { get; set; }

        public dynamic data { get; set; }
    }

    [JsonObject]
    public class UserProfile
    {
        [JsonProperty("last_access_date")]
        public DateTime LastAccessDate { get; set; }

        [JsonProperty("sms")]
        public string Sms { get; set; }

        [JsonProperty("disk_quota")]
        public long DiskQuota { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("disk_usage")]
        public string DiskUsage { get; set; }

        [JsonProperty("contact")]
        public string Contact { get; set; }

        [JsonProperty("domainname")]
        public string Domainname { get; set; }

        [JsonProperty("custom3")]
        public string Custom3 { get; set; }

        [JsonProperty("state")]
        public string State { get; set; }

        [JsonProperty("over_quota")]
        public long OverQuota { get; set; }

        [JsonProperty("city")]
        public string City { get; set; }

        [JsonProperty("fax")]
        public string Fax { get; set; }

        [JsonProperty("user")]
        public string User { get; set; }

        [JsonProperty("company")]
        public string Company { get; set; }

        [JsonProperty("country")]
        public string Country { get; set; }

        [JsonProperty("uid")]
        public string Uid { get; set; }

        [JsonProperty("flags")]
        public string[] Flags { get; set; }

        [JsonProperty("email2")]
        public string Email2 { get; set; }

        [JsonProperty("services")]
        public string[] Services { get; set; }

        [JsonProperty("email1")]
        public string Email1 { get; set; }

        [JsonProperty("street1")]
        public string Street1 { get; set; }

        [JsonProperty("custom1")]
        public string Custom1 { get; set; }

        [JsonProperty("modified")]
        public DateTime Modified { get; set; }

        [JsonProperty("created")]
        public DateTimeOffset Created { get; set; }

        [JsonProperty("zip")]
        public string Zip { get; set; }

        [JsonProperty("custom2")]
        public string Custom2 { get; set; }

        [JsonProperty("phone1")]
        public string Phone1 { get; set; }

        [JsonProperty("street2")]
        public string Street2 { get; set; }

        [JsonProperty("phone2")]
        public string Phone2 { get; set; }
    }

    // class to hold Email Request
    [JsonObject]
    public partial class EmailRequest
    {
        [JsonProperty("attachments")] public Attachment[] Attachments { get; set; }

        [JsonProperty("from_address")] public string FromAddress { get; set; }

        [JsonProperty("subject")] public string Subject { get; set; }

        [JsonProperty("no_tls_only")] public long NoTlsOnly { get; set; }

        [JsonProperty("body")] public string Body { get; set; }

        [JsonProperty("to")] public string[] To { get; set; }

        [JsonProperty("from_name")] public string FromName { get; set; }

        [JsonProperty("receipt")] public long Receipt { get; set; }

        [JsonProperty("body_type")] public string BodyType { get; set; } // must be "text" or "html"
    }

    // class to hold attachment
    [JsonObject]
    public partial class Attachment
    {
        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }


}
