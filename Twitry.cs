// Using Twitry & DynamicJson
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Security.Cryptography;
using System.Web;
using System.Xml;
using System.IO;
using System.Collections;
using System.Drawing;
using System.Timers;
using System.Threading;
using System.Runtime.Serialization;

// Using DynamicJson
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Xml.Linq;

using Codeplex.Data;
using Twitry;

namespace Twitry
{
    class Twitter
    {

        /*          [ 使い方 ]
         *  1. OAuth関数を実行。
         *  2. CheckTwitter関数を実行(任意)
         *  3. CheckAccount関数を実行(絶対)
         *  4. GetAPI関数でAPI叩く
         *  5.(ﾟдﾟ)ｳﾏｰ
         */

        private const string RequestTokenURL = "https://api.twitter.com/oauth/request_token";
        private const string AuthorizeURL = "https://api.twitter.com/oauth/authorize";
        private const string AccessTokenURL = "https://api.twitter.com/oauth/access_token";

        private const string ConsumerKey = "Ji0zcRyyjTWh8O00AfiqA";
        private const string ConsumerSecret = "6xQL0wiMinEtAwBrp8aR6FOIrkcweEzC8Y8jqdHsDaM";

        public string AccessToken = "";
        public string AccessTokenSecret = "";

        public User UserData;

        private string OauthToken = "";
        private string OauthTokenSecret = "";

        private bool AuthCheck = false;
        private bool GetOAuth = false;

        private IAsyncResult UserStreamAsync = null;
        private IAsyncResult UserStreamReadAsync = null;
        private byte[] UserStreamResultBuffer = null;


        /// <summary>
        /// アクセスしたいユーザーを作る。
        /// </summary>
        /// <param name="AccessToken">アクセストークン</param>
        /// <param name="AccessTokenSecret">アクセストークンシークレット</param>
        public Twitter(string AccessToken, string AccessTokenSecret)
        {
            this.AccessToken = AccessToken;
            this.AccessTokenSecret = AccessTokenSecret;
        }
        /// <summary>
        /// アクセスしたいユーザーを作る。
        /// (AccessToken、AccessTokenSecretがない場合)
        /// </summary>
        public Twitter()
        {
        }

        /// <summary>
        /// Twitter生存確認
        /// </summary>
        /// <returns>生きてるならTrue</returns>
        public bool CheckTwitter()
        {
            WebClient wc = new WebClient();
            string result = "";
            try
            {
                result = wc.DownloadString("http://api.twitter.com/1/help/test.json");
            }
            catch (WebException)
            {
                return false;
            }
            if (result == "\"ok\"")
                return true;
            return false;
        }

        /// <summary>
        /// アカウントにアクセスできるか確認する
        /// </summary>
        /// <returns>アクセスできたらTrue</returns>
        public bool CheckAccount()
        {
            string result = (string)GetAPI("http://api.twitter.com/1/account/verify_credentials.json").result;
            if (result.IndexOf("<error>") != -1)
                return false;
            AuthCheck = true;
            return true;
        }

        /// <summary>
        /// OAuth認証用のURLを取得する
        /// </summary>
        /// <returns>URL、または、エラー</returns>
        public ReturnMessage GetOAuthRequestURL()
        {
            GetOAuth = true;
            ReturnMessage APIresult = PostAPI("https://api.twitter.com/oauth/request_token");
            ReturnMessage result = CreateReturnMessage(0, "", "");
            if (APIresult.No == 0)
            {
                string[] Items = ((string)APIresult.result).Split('&');
                Dictionary<string, string> ItemDic = new Dictionary<string, string>();
                foreach (string buff in Items)
                {
                    string[] a = buff.Split('=');
                    ItemDic.Add(a[0], a[1]);
                }
                result.result = "http://twitter.com/oauth/authorize?oauth_token=" + ItemDic["oauth_token"];
                OauthToken = ItemDic["oauth_token"];
                OauthTokenSecret = ItemDic["oauth_token_secret"];
            }
            return result;
        }

        /// <summary>
        /// PINコードからアクセストークンを取得する関数
        /// 先にGetOAuthRequestURLを実行すること。
        /// </summary>
        /// <param name="PIN">PINコード</param>
        /// <returns>エラー</returns>
        public ReturnMessage GetAccessToken(string PIN)
        {
            ReturnMessage result = CreateReturnMessage();
            if (GetOAuth)
            {
                Dictionary<string, string> dic = new Dictionary<string, string>();
                dic.Add("oauth_verifier", PIN);
                ReturnMessage apiresult = PostAPI("https://api.twitter.com/oauth/access_token", dic);
                if (apiresult.No != 0)
                    result = apiresult;
                else
                {
                    Dictionary<string, string> dic2 = new Dictionary<string, string>();
                    string[] a = ((string)apiresult.result).Split('&');
                    foreach (string buff in a)
                    {
                        string[] buff2 = buff.Split('=');
                        dic2.Add(buff2[0], buff2[1]);
                    }
                    AccessToken = dic2["oauth_token"];
                    AccessTokenSecret = dic2["oauth_token_secret"];
                    OauthToken = "";
                    OauthTokenSecret = "";
                    GetOAuth = false;
                }
            }
            else
            {
                result.No = 5;
                result.Message = "Not going to get the Request URL.";
                result.result = "";
            }
            return result;
        }

        /// <summary>
        /// GETする
        /// </summary>
        /// <param name="URL">GETしたいURL</param>
        /// <param name="query">ヘッダに追加したいデータ</param>
        /// <returns>取得してきたデータ、または、エラー</returns>
        public ReturnMessage GetAPI(string URL, Dictionary<string, string> query = null)
        {
            ReturnMessage result = Check();
            if (result.No == 0)
            {
                string postText = "";
                SortedDictionary<string, string> sd;
                if (query == null)
                    sd = new SortedDictionary<string, string>();
                else
                    sd = new SortedDictionary<string, string>(query);
                sd.Add("oauth_consumer_key", ConsumerKey);
                sd.Add("oauth_signature_method", "HMAC-SHA1");
                sd.Add("oauth_timestamp", GenereteTimeStamp());
                sd.Add("oauth_nonce", GenereteNonce());
                if (GetOAuth == false)
                    sd.Add("oauth_token", AccessToken);
                else
                    sd.Add("oauth_token", OauthToken);
                sd.Add("oauth_version", "1.0");
                string buff = UrlEncodeSmall(GenereteSignature("GET", URL, sd));
                sd.Add("oauth_signature", buff);

                postText = SortedDictionary2String(sd, "&", "");

                WebRequest req = WebRequest.Create(string.Format("{0}?{1}", URL, postText));
                WebResponse res;
                result.result = "";
                try
                {
                    res = req.GetResponse();
                    Stream stream = res.GetResponseStream();
                    StreamReader reader = new StreamReader(stream);
                    result.result = reader.ReadToEnd();
                    reader.Close();
                    stream.Close();
                }
                catch (WebException ex)
                {
                    result.No = -1;
                    result.Message = "WebException.";
                    result.result = ex.Message;
                }
            }
            return result;
        }

        /// <summary>
        /// POSTする
        /// </summary>
        /// <param name="URL">POSTしたいURL</param>
        /// <param name="query">クエリに追加したいデータ</param>
        /// <returns>取得してきたデータ、または、エラー</returns>
        public ReturnMessage PostAPI(string URL, Dictionary<string, string> query = null)
        {
            ReturnMessage result = Check();
            if (result.No == 0)
            {
                string postText = "";
                SortedDictionary<string, string> sd;
                if (query == null)
                    sd = new SortedDictionary<string, string>();
                else
                    sd = new SortedDictionary<string, string>(query);

                if (sd.ContainsKey("status"))
                    sd["status"] = UrlEncode(sd["status"]);

                sd.Add("oauth_consumer_key", ConsumerKey);
                sd.Add("oauth_signature_method", "HMAC-SHA1");
                sd.Add("oauth_timestamp", GenereteTimeStamp());
                sd.Add("oauth_nonce", GenereteNonce());
                if (GetOAuth == false)
                    sd.Add("oauth_token", AccessToken);
                else
                    sd.Add("oauth_token", OauthToken);
                sd.Add("oauth_version", "1.0");
                string buff = UrlEncodeSmall(GenereteSignature("POST", URL, sd));
                sd.Add("oauth_signature", buff);

                postText = SortedDictionary2String(sd, "&", "");

                WebRequest req = WebRequest.Create(URL);
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = postText.Length;
                Stream reqStream = req.GetRequestStream();
                reqStream.Write(Encoding.ASCII.GetBytes(postText), 0, postText.Length);
                reqStream.Close();
                WebResponse res;
                result.result = "";
                try
                {
                    res = req.GetResponse();
                    Stream stream = res.GetResponseStream();
                    StreamReader reader = new StreamReader(stream);
                    result.result = reader.ReadToEnd();
                    reader.Close();
                    stream.Close();
                }
                catch (WebException ex)
                {
                    result.No = -1;
                    result.Message = "WebException";
                    result.result = ex.Message;
                }
            }
            return result;
        }

        /// <summary>
        /// 全く関連性のない文字列を生成する
        /// </summary>
        /// <returns>全く関係ない文字列(string)</returns>
        private string GenereteNonce()
        {
            string parts = "abcdefghjklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string result = "";
            Random rm = new Random();
            for (int i = 0; i < 30; ++i)
                result += parts[rm.Next(parts.Length)];
            return result;
        }

        /// <summary>
        /// URLエンコーディング(大文字)に変換する
        /// </summary>
        /// <param name="value">URLエンコーディングしたい文字列</param>
        /// <returns>URLエンコード(大文字)した文字列(string)</returns>
        private string UrlEncode(string value)
        {
            string unreserved = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
            StringBuilder result = new StringBuilder();
            byte[] data = Encoding.UTF8.GetBytes(value);
            foreach (byte b in data)
            {
                if (b < 0x80 && unreserved.IndexOf((char)b) != -1)
                    result.Append((char)b);
                else
                    result.Append('%' + String.Format("{0:X2}", (int)b));
            }
            return result.ToString();
        }
        /// <summary>
        /// URLエンコーディング(小文字)に変換する
        /// </summary>
        /// <param name="value">URLエンコーディングしたい文字列</param>
        /// <returns>URLエンコード(小文字)した文字列(string)</returns>
        private string UrlEncodeSmall(string value)
        {
            string unreserved = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

            StringBuilder result = new StringBuilder();
            byte[] data = Encoding.UTF8.GetBytes(value);
            foreach (byte b in data)
            {
                if (b < 0x80 && unreserved.IndexOf((char)b) != -1)
                    result.Append((char)b);
                else
                    result.Append('%' + String.Format("{0:x2}", (int)b));
            }
            return result.ToString();
        }

        /// <summary>
        /// Unix時間(1970/01/01/ 00:00:00からの秒数)を取得する
        /// </summary>
        /// <returns>今のUnix時間(文字列)</returns>
        private string GenereteTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }

        /// <summary>
        ///    Signatureを生成する(正直良くわからん。)
        /// </summary>
        /// <param name="ReqestText">クエリ(GET,POST...)</param>
        /// <param name="URLText">リクエストをする予定のURL</param>
        /// <param name="AuthorizationDictionary">ヘッダに追加するデータ</param>
        /// <returns>Signature(string型)</returns>
        private string GenereteSignature(string ReqestText, string URLText, SortedDictionary<string, string> AuthorizationDictionary)
        {
            string buff = ReqestText + "&" + UrlEncode(URLText) + "&" + UrlEncode(SortedDictionary2String(AuthorizationDictionary, "&", ""));

            var hmacsha1 = new System.Security.Cryptography.HMACSHA1();
            if (GetOAuth == false)
                hmacsha1.Key = System.Text.Encoding.ASCII.GetBytes(UrlEncode(ConsumerSecret) + "&" + UrlEncode(AccessTokenSecret));
            else
                hmacsha1.Key = System.Text.Encoding.ASCII.GetBytes(UrlEncode(ConsumerSecret) + "&" + UrlEncode(OauthTokenSecret));
            string a = UrlEncode(ConsumerSecret) + "&" + UrlEncode(AccessTokenSecret);
            var dataBuffer = Encoding.UTF8.GetBytes(buff);
            var hashBytes = hmacsha1.ComputeHash(dataBuffer);
            var signature = Convert.ToBase64String(hashBytes);

            return signature;
        }

        /// <summary>
        ///    SortedDictionaryを指定した文字列でくっつけた文字列を出力する
        /// </summary>
        /// <param name="SD">stringに変えたいSortedDictionary</param>
        /// <param name="SeparateText">キーと値の間に入れる文字</param>
        /// <param name="IncloseText">値の両端につける文字</param>
        /// <returns>くっつけた文字列</returns>
        private string SortedDictionary2String(SortedDictionary<string, string> SD, string SeparateText, string IncloseText = "\"")
        {
            string result = "";
            int count = SD.Count;
            foreach (KeyValuePair<string, string> pair in SD)
            {
                count--;
                result += pair.Key + "=" + IncloseText + pair.Value + IncloseText;
                if (count != 0)
                {
                    result += SeparateText;
                }
            }
            return result;
        }


        private ReturnMessage Check()
        {
            ReturnMessage result;
            if (GetOAuth == false)
            {
                if (AuthCheck == false) result = CreateReturnMessage(2, "Not AccessToken Check.", "");
                else if (AccessToken == "" || AccessTokenSecret == "") result = CreateReturnMessage(1, "Not AccessToken.", "");
                else result = CreateReturnMessage(0, "", "");
            }
            else
                result = CreateReturnMessage(0, "", "");

            return result;
        }
        private ReturnMessage CreateReturnMessage(int No = 0, string Message = "", string result = "")
        {
            ReturnMessage a = new ReturnMessage();
            a.No = No;
            a.Message = Message;
            a.result = result;
            return a;
        }
        private Target CreateTarget(string EventString, int Num, object Data, DateTime Date)
        {
            Target result = new Target();
            result.Event = EventString;
            result.EventNum = Num;
            result.Object = Data;
            result.Time = Date;
            return result;
        }
        private DateTime DateStringToDataTime(string TwitterDateString)
        {
            return DateTime.ParseExact(TwitterDateString,
                                               "ddd MMM dd HH:mm:ss K yyyy",
                                               System.Globalization.DateTimeFormatInfo.InvariantInfo);
        }

        /// <summary>
        /// UserStreamを開始する。
        /// </summary>
        /// <returns>エラー</returns>
        public ReturnMessage StartUserStream()
        {
            ReturnMessage result = Check();
            string URL = "https://userstream.twitter.com/2/user.json";
            if (result.No == 0)
            {
                string postText = "";
                SortedDictionary<string, string> sd;
                sd = new SortedDictionary<string, string>();

                sd.Add("oauth_consumer_key", ConsumerKey);
                sd.Add("oauth_signature_method", "HMAC-SHA1");
                sd.Add("oauth_timestamp", GenereteTimeStamp());
                sd.Add("oauth_nonce", GenereteNonce());
                sd.Add("oauth_token", AccessToken);
                sd.Add("oauth_version", "1.0");
                string buff = UrlEncodeSmall(GenereteSignature("POST", URL, sd));
                sd.Add("oauth_signature", buff);

                postText = SortedDictionary2String(sd, "&", "");

                WebRequest req = WebRequest.Create(URL);
                req.Method = "POST";
                req.ContentType = "application/x-www-form-urlencoded";
                req.ContentLength = postText.Length;
                Stream reqStream = req.GetRequestStream();
                reqStream.Write(Encoding.ASCII.GetBytes(postText), 0, postText.Length);
                reqStream.Close();
                try
                {
                    UserStreamAsync = req.BeginGetResponse(new AsyncCallback(CallBack), req);

                }
                catch (WebException ex)
                {
                    result.No = -1;
                    result.Message = "WebException.";
                    result.result = ex.Message;
                }
            }
            return result;
        }

        /// <summary>
        /// UserStreamを強制的に停止します。
        /// </summary>
        /// <returns>エラー</returns>
        public void EndUserStream()
        {
            ((WebRequest)UserStreamAsync.AsyncState).Abort();
            ((Stream)UserStreamReadAsync.AsyncState).Close();
        }

        private void CallBack(IAsyncResult ar)
        {
            try
            {
                UserStreamResultBuffer = new byte[8096];
                WebRequest req = (WebRequest)ar.AsyncState;
                WebResponse res = (WebResponse)req.EndGetResponse(ar);
                Stream sr = res.GetResponseStream();
                UserStreamReadAsync = sr.BeginRead(UserStreamResultBuffer, 0, UserStreamResultBuffer.Length, new AsyncCallback(StreamCallBack), sr);
                //sr.Close();
                //Console.Write(UserStreamResultBuffer);
            }
            catch (WebException e)
            {
                Console.WriteLine("Error\n" + e.Message);
                StreamReader st = new StreamReader(e.Response.GetResponseStream());
                Console.WriteLine(st.ReadToEnd());
            }
            return;
        }
        private void StreamCallBack(IAsyncResult ar)
        {
            try
            {
                Stream st = (Stream)ar.AsyncState;
                int read = st.EndRead(ar);
                StreamEvent(Encoding.UTF8.GetString(UserStreamResultBuffer, 0, read));
                st.BeginRead(UserStreamResultBuffer, 0, UserStreamResultBuffer.Length, new AsyncCallback(StreamCallBack), st);
            }
            catch (Exception)
            {
            }
        }
        /// <summary>
        /// 誰かがTweetした時に発生するイベント
        /// </summary>
        public event AddTweetHandler AddTweet = delegate { };
        /// <summary>
        /// フォローしている人の一覧が流れた時に発生するイベント
        /// </summary>
        public event FriendHandler Friend = delegate { };
        /// <summary>
        /// 誰かがTweetを消した時に発生するイベント
        /// </summary>
        public event DeleteTweetHandler DeleteTweet = delegate { };
        /// <summary>
        /// その他のイベントが発生した時に発生するイベント
        /// </summary>
        public event TargetHandler TargetEvent = delegate { };

        protected virtual void StreamEvent(string TweetJson)
        {
            if (TweetJson.Length >= 4)
            {
                var twitterjson = DynamicJson.Parse(TweetJson);
                StreamWriter sw = File.AppendText("log.txt");
                sw.WriteLine(TweetJson);
                sw.Close();
                //string mid = TweetJson.Substring(0, 20);
                if (twitterjson.IsDefined("friends"))
                {
                    List<UInt64> FriendIDs = new List<ulong>();
                    foreach (ulong item in twitterjson["friends"])
                    {
                        FriendIDs.Add(item);
                    }
                    Friend(FriendIDs);
                }
                if (twitterjson.IsDefined("text") && !twitterjson.IsDefined("event"))
                {
                    TweetAUser tw = new TweetAUser();

                    tw.Tweet = new Tweet(TweetJson);

                    var userjson = twitterjson["user"].ToString();

                    tw.User = new User(userjson);
                    AddTweet(tw);
                }
                if (twitterjson.IsDefined("event"))
                {
                    //Console.WriteLine("誰かが何かをしました。");
                    Target result;
                    UserAUser UAU;
                    TweetAUser TAU;
                    ListAUser LAU;
                    UserList UL;
                    User U;
                    Tweet T;
                    switch ((string)twitterjson["event"])
                    {
                        case "follow":
                            U = new User(twitterjson["source"].ToString());
                            result = CreateTarget("Follow", 1, U, DateStringToDataTime(twitterjson["created_at"]));
                            TargetEvent(result);
                            break;
                        case "user_update":
                            U = new User(twitterjson["target"].ToString());
                            result = CreateTarget("UserUpdate", 2, U, DateStringToDataTime(twitterjson["created_at"]));
                            TargetEvent(result);
                            break;
                        case "block":
                            U = new User(twitterjson["source"].ToString());
                            result = CreateTarget("Block", 3, U, DateStringToDataTime(twitterjson["created_at"]));
                            TargetEvent(result);
                            break;
                        case "unblock":
                            U = new User(twitterjson["source"].ToString());
                            result = CreateTarget("Block", 3, U, DateStringToDataTime(twitterjson["created_at"]));
                            TargetEvent(result);
                            break;
                        case "favorite":
                            TAU = new TweetAUser();
                            TAU.Tweet = new Tweet(twitterjson["target_object"].ToString());
                            TAU.User = new User(twitterjson["source"].ToString());
                            result = CreateTarget("Favorite", 4, TAU, DateStringToDataTime(twitterjson["created_at"]));
                            TargetEvent(result);
                            break;
                        case "unfavorite":
                            TAU = new TweetAUser();
                            TAU.Tweet = new Tweet(twitterjson["target_object"].ToString());
                            TAU.User = new User(twitterjson["source"].ToString());
                            result = CreateTarget("Favorite", 4, TAU, DateStringToDataTime(twitterjson["created_at"]));
                            TargetEvent(result);
                            break;
                        case "list_created":
                            UL = new UserList(twitterjson["target_object"].ToString());
                            result = CreateTarget("ListCreated", 5, UL, DateStringToDataTime(twitterjson["created_at"]));
                            TargetEvent(result);
                            break;
                        case "list_destroyed":
                            UL = new UserList(twitterjson["target_object"].ToString());
                            result = CreateTarget("ListDestroyed", 6, UL, DateStringToDataTime(twitterjson["created_at"]));
                            TargetEvent(result);
                            break;
                        default:
                            Console.WriteLine("何かしたそうです。\n" + twitterjson["event"]);
                            break;
                    }
                    //FollowUser(null);
                }
                if (twitterjson.IsDefined("delete"))
                {
                    Console.WriteLine("delete");
                    DeleteTweet((ulong)twitterjson["delete"]["status"]["id"]);
                }
                //Console.WriteLine(TweetJson);
                //Console.WriteLine(mid);
            }
        }
    }
    public struct ReturnMessage
    {
        /*
            *     エラーナンバー
            *  番号一覧 -1 : 不明なエラー
            *           0  : エラー無し
            *           1  : AccessToken,AccessSecretが無い。
            *           2  : AccessTokenをテストしていない。または、テストしたが認証が通らなかった。
            *           3  : API切れ
            *           4  : 何かが取得できなかった
            *           5  : 一度もRequestURLを取得しに行っていない。
            */
        public int No;

        public string Message;
        public object result;
    }
    public struct Source
    {
        public Uri URL;
        public string Name;
    }
    public class Tweet
    {
        public string Status, ReplyScreenName;
        public DateTime CreatedAt;
        public UInt64 ID, ReplyStatusID, ReplyUserID, UserID, ReTweetID;
        public bool UserFavorited, UserRetweeted, Retweeted, Truncated;
        public Source Source;

        public Tweet(XmlNode StatusXML)
        {
            if (StatusXML.Name == "status")
            {
                CreatedAt = DateTime.ParseExact(StatusXML["created_at"].InnerText,
                                               "ddd MMM dd HH:mm:ss K yyyy",
                                               System.Globalization.DateTimeFormatInfo.InvariantInfo);
                Status = StatusXML["text"].InnerText;
                ID = UInt64.Parse(StatusXML["id"].InnerText);
                Source = new Source();
                if (StatusXML["source"].FirstChild.Name == "a")
                {
                    Source.Name = StatusXML["source"]["a"].InnerText;
                    Source.URL = new Uri(StatusXML["source"]["a"].Attributes["href"].InnerText);
                }
                else
                    Source.Name = StatusXML["source"].InnerText;
                Truncated = bool.Parse(StatusXML["truncated"].InnerText);
                if (StatusXML["in_reply_to_status_id"].InnerText != "")
                    ReplyStatusID = UInt64.Parse(StatusXML["in_reply_to_status_id"].InnerText);
                if (StatusXML["in_reply_to_user_id"].InnerText != "")
                    ReplyUserID = UInt64.Parse(StatusXML["in_reply_to_user_id"].InnerText);
                ReplyScreenName = StatusXML["in_reply_to_screen_name"].InnerText;
                UserID = UInt64.Parse(StatusXML["user"]["id"].InnerText);
                UserFavorited = bool.Parse(StatusXML["favorited"].InnerText);
                UserRetweeted = bool.Parse(StatusXML["retweeted"].InnerText);
                if (StatusXML["retweeted_status"] != null)
                {
                    Retweeted = true;
                    ReTweetID = UInt64.Parse(StatusXML["retweeted_status"]["id"].InnerText);
                }
            }
        }
        //public Tweet(string StatusXMLString)
        //{
        //    XmlDocument StatusXML = new XmlDocument();
        //    StatusXML.LoadXml(StatusXMLString);
        //    if (StatusXML.Name == "status")
        //    {
        //        CreateAt = DateTime.ParseExact(StatusXML["created_at"].InnerText,
        //                                       "ddd MMM dd HH:mm:ss K yyyy",
        //                                       System.Globalization.DateTimeFormatInfo.InvariantInfo);
        //        Status = StatusXML["text"].InnerText;
        //        ID = UInt64.Parse(StatusXML["id"].InnerText);
        //        Source = new Source();
        //        if (StatusXML["source"].FirstChild.Name == "a")
        //        {
        //            Source.Name = StatusXML["source"]["a"].InnerText;
        //            Source.URL = new Uri(StatusXML["source"]["a"].Attributes["href"].InnerText);
        //        }
        //        else
        //            Source.Name = StatusXML["source"].InnerText;
        //        Truncated = bool.Parse(StatusXML["truncated"].InnerText);
        //        if (StatusXML["in_reply_to_status_id"].InnerText != "")
        //            ReplyStatusID = UInt64.Parse(StatusXML["in_reply_to_status_id"].InnerText);
        //        if (StatusXML["in_reply_to_user_id"].InnerText != "")
        //            ReplyUserID = UInt64.Parse(StatusXML["in_reply_to_user_id"].InnerText);
        //        ReplyScreenName = StatusXML["in_reply_to_screen_name"].InnerText;
        //        UserID = UInt64.Parse(StatusXML["user"]["id"].InnerText);
        //        UserFavorited = bool.Parse(StatusXML["favorited"].InnerText);
        //        UserRetweeted = bool.Parse(StatusXML["retweeted"].InnerText);
        //        if (StatusXML["retweeted_status"] != null)
        //        {
        //            Retweeted = true;
        //            ReTweetID = UInt64.Parse(StatusXML["retweeted_status"]["id"].InnerText);
        //        }
        //    }
        //}
        public Tweet(string StatusJsonString)
        {
            var tweet = DynamicJson.Parse(StatusJsonString);

            Status = tweet["text"];
            ReplyScreenName = tweet["in_reply_to_screen_name"];
            CreatedAt = DateTime.ParseExact(tweet["created_at"],
                                               "ddd MMM dd HH:mm:ss K yyyy",
                                               System.Globalization.DateTimeFormatInfo.InvariantInfo);
            ID = (ulong)tweet["id"];
            if (tweet["in_reply_to_status_id"] != null)
                ReplyStatusID = (ulong)tweet["in_reply_to_status_id"];
            if (tweet["in_reply_to_user_id"] != null)
                ReplyUserID = (ulong)tweet["in_reply_to_user_id"];
            UserID = (ulong)tweet["user"]["id"];
            if (tweet.IsDefined("retweet_status") == true)
            {
                Retweeted = true;
                ReTweetID = (ulong)tweet["retweet_status"]["id"];
            }
            UserFavorited = tweet["favorited"];
            UserRetweeted = tweet["retweeted"];
            Truncated = tweet["truncated"];
            XmlDocument xd = new XmlDocument();
            try
            {
                xd.LoadXml(tweet["source"]);
                Source.Name = xd["a"].InnerText;
                Source.URL = new Uri(xd["a"].Attributes["href"].InnerText);
            }
            catch (XmlException)
            {
                Source.Name = tweet["source"];
            }

            //JsonContract.TweetJsonContract tweet = JsonConvert.DeserializeObject<JsonContract.TweetJsonContract>(StatusJsonString);

            //Status = tweet.Status;
            //ReplyScreenName = tweet.ReplyScreenName;
            //CreateAt = DateTime.ParseExact(tweet.CreateAt,
            //                                   "ddd MMM dd HH:mm:ss K yyyy",
            //                                   System.Globalization.DateTimeFormatInfo.InvariantInfo);
            //ID = tweet.TweetID;
            //if (tweet.ReplyStatusID != null)
            //    ReplyStatusID = UInt64.Parse(tweet.ReplyStatusID);
            //if (tweet.ReplyUserID != null)
            //    ReplyUserID = UInt64.Parse(tweet.ReplyUserID);
            //UserID = tweet.User.ID;
            //if (tweet.RetweetStatus != null)
            //{
            //    Retweeted = true;
            //    ReTweetID = tweet.RetweetStatus.TweetID;
            //}
            //UserFavorited = tweet.Favorited;
            //UserRetweeted = tweet.Retweeted;
            //Truncated = tweet.Truncated;
            //XmlDocument xd = new XmlDocument();
            //try
            //{
            //    xd.LoadXml(tweet.Source);
            //    Source.Name = xd["a"].InnerText;
            //    Source.URL = new Uri(xd["a"].Attributes["href"].InnerText);
            //}
            //catch (XmlException)
            //{
            //    Source.Name = tweet.Source;
            //}

            //XmlDictionaryReader tw = JsonReaderWriterFactory.CreateJsonReader(Encoding.Default.GetBytes(StatusJsonString), XmlDictionaryReaderQuotas.Max);
            //XmlDictionaryReader us = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(tw["user"]), XmlDictionaryReaderQuotas.Max);
            //Status = tw["text"];
            //ReplyScreenName = tw["in_reply_to_screen_name"];
            //if (tw["retweet_status"] != null)
            //{
            //    Retweeted = true;
            //    ReTweetID = UInt64.Parse(tw["retweet_status"]);
            //}
            //CreateAt = DateTime.ParseExact(tw["create_at"],
            //                                   "ddd MMM dd HH:mm:ss K yyyy",
            //                                   System.Globalization.DateTimeFormatInfo.InvariantInfo);
            //ID = UInt64.Parse(tw["id"]);
            //if (tw["in_reply_to_status_id"] != null)
            //    ReplyStatusID = UInt64.Parse(tw["in_reply_to_status_id"]);
            //if (tw["in_reply_to_user_id"] != null)
            //    ReplyUserID = UInt64.Parse(tw["in_reply_to_user_id"]);
            //UserID = UInt64.Parse(us["id"]);

            //UserFavorited = bool.Parse(tw["favorited"]);
            //UserRetweeted = bool.Parse(tw["retweeted"]);
            //Truncated = bool.Parse(tw["retweeted"]);
            //XmlDocument xd = new XmlDocument();
            //xd.LoadXml(tw["source"]);
            //if (xd.FirstChild.Name == "a")
            //{
            //    Source.Name = xd["a"].InnerText;
            //    Source.URL = new Uri(xd["a"].Attributes["href"].InnerText);
            //}
            //else
            //    Source.Name = tw["source"];

            //StatusJsonString = StatusJsonString.Replace("href=\"h", "href=\\\"h").Replace("\" rel=\"", "\\\" rel=\\\"").Replace("\">", "\\\">");
            //DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(JsonContract.TweetJsonContract));
            //Stream st = new MemoryStream(Encoding.UTF8.GetBytes(StatusJsonString));
            //JsonContract.TweetJsonContract jstw = (JsonContract.TweetJsonContract)serializer.ReadObject(st);
            //Stream st2 = new MemoryStream(Encoding.UTF8.GetBytes(jstw.User));
            //JsonContract.UserJsonContract jsur = (JsonContract.UserJsonContract)serializer.ReadObject(st2);
            //Stream st3 = new MemoryStream(Encoding.UTF8.GetBytes(jstw.RetweetStatus));
            //JsonContract.TweetJsonContract jsrt = (JsonContract.TweetJsonContract)serializer.ReadObject(st3);
            //Status = jstw.Status;
            //ReplyScreenName = jstw.ReplyScreenName;
            //CreateAt = DateTime.ParseExact(jstw.CreateAt,
            //                                   "ddd MMM dd HH:mm:ss K yyyy",
            //                                   System.Globalization.DateTimeFormatInfo.InvariantInfo);
            //ID = (ulong)jstw.TweetID;
            //if (jstw.ReplyStatusID != null)
            //    ReplyStatusID = ulong.Parse(jstw.ReplyStatusID);
            //if (jstw.ReplyUserID != null)
            //    ReplyUserID = ulong.Parse(jstw.ReplyUserID);
            //UserID = (ulong)jsur.ID;
            //if (jstw.RetweetStatus != null)
            //{
            //    Retweeted = true;
            //    ReTweetID = (ulong)jsrt.TweetID;
            //}

            //UserFavorited = jstw.Favorited;
            //UserRetweeted = jstw.Retweeted;
            //Truncated = jstw.Truncated;
            //XmlDocument xd = new XmlDocument();
            //xd.LoadXml(jstw.Source);
            //if (xd.FirstChild.Name == "a")
            //{
            //    Source.Name = xd["a"].InnerText;
            //    Source.URL = new Uri(xd["a"].Attributes["href"].InnerText);
            //}
            //else
            //    Source.Name = jstw.Source;
        }
        //public Tweet(JsonContract.TweetJsonContract tweet)
        //{
        //    Status = tweet.Status;
        //    ReplyScreenName = tweet.ReplyScreenName;
        //    CreateAt = DateTime.ParseExact(tweet.CreateAt,
        //                                       "ddd MMM dd HH:mm:ss K yyyy",
        //                                       System.Globalization.DateTimeFormatInfo.InvariantInfo);
        //    ID = tweet.TweetID;
        //    if (tweet.ReplyStatusID != null)
        //        ReplyStatusID = UInt64.Parse(tweet.ReplyStatusID);
        //    if (tweet.ReplyUserID != null)
        //        ReplyUserID = UInt64.Parse(tweet.ReplyUserID);
        //    UserID = tweet.User.ID;
        //    if (tweet.RetweetStatus != null)
        //    {
        //        Retweeted = true;
        //        ReTweetID = tweet.RetweetStatus.TweetID;
        //    }
        //    UserFavorited = tweet.Favorited;
        //    UserRetweeted = tweet.Retweeted;
        //    Truncated = tweet.Truncated;
        //}
        //public Tweet(string status)
        //{
        //    Status = status;
        //}
        public Tweet(string status, UInt64 id = 0)
        {
            Status = status;
            ID = id;
        }
    }
    public class User
    {
        public UInt64 UserID;
        public string Name, ScreenName, Location, Description;
        public Image ProfileImage;
        public Uri Uri;
        public bool Protected, Verified;
        public int Friend, Follower, Favote, Status;
        public DateTime Admission;

        public User()
        {
        }
        public User(XmlNode UserXML)
        {
            if (UserXML.Name == "user")
            {
                UserID = UInt64.Parse(UserXML["id"].InnerText);
                Name = UserXML["name"].InnerText;
                ScreenName = UserXML["screen_name"].InnerText;
                Location = UserXML["location"].InnerText;
                Description = UserXML["description"].InnerText;
                if (UserXML["profile_image_url"].InnerText != "")
                {
                    WebClient wc = new WebClient();
                    Stream stream = wc.OpenRead(UserXML["profile_image_url"].InnerText);
                    ProfileImage = Image.FromStream(stream);
                    stream.Dispose();
                    wc.Dispose();
                }
                if (UserXML["url"].InnerText != "")
                    Uri = new Uri(UserXML["url"].InnerText);
                Protected = bool.Parse(UserXML["protected"].InnerText);
                Verified = bool.Parse(UserXML["verified"].InnerText);
                Friend = int.Parse(UserXML["friends_count"].InnerText);
                Follower = int.Parse(UserXML["followers_count"].InnerText);
                Favote = int.Parse(UserXML["favourites_count"].InnerText);
                Status = int.Parse(UserXML["statuses_count"].InnerText);
                Admission = DateTime.ParseExact(UserXML["created_at"].InnerText,
                                                "ddd MMM dd HH:mm:ss K yyyy",
                                                System.Globalization.DateTimeFormatInfo.InvariantInfo);
            }
        }
        //public User(string UserXMLString)
        //{
        //    XmlDocument UserXML = new XmlDocument();
        //    UserXML.LoadXml(UserXMLString);
        //    if (UserXML.Name == "user")
        //    {
        //        UserID = UInt64.Parse(UserXML["id"].InnerText);
        //        Name = UserXML["name"].InnerText;
        //        ScreenName = UserXML["screen_name"].InnerText;
        //        Location = UserXML["location"].InnerText;
        //        Description = UserXML["description"].InnerText;
        //        if (UserXML["profile_image_url"].InnerText != "")
        //        {
        //            WebClient wc = new WebClient();
        //            Stream stream = wc.OpenRead(UserXML["profile_image_url"].InnerText);
        //            ProfileImage = Image.FromStream(stream);
        //            stream.Dispose();
        //            wc.Dispose();
        //        }
        //        if (UserXML["url"].InnerText != "")
        //            Uri = new Uri(UserXML["url"].InnerText);
        //        Protected = bool.Parse(UserXML["protected"].InnerText);
        //        Verified = bool.Parse(UserXML["verified"].InnerText);
        //        Friend = int.Parse(UserXML["friends_count"].InnerText);
        //        Follower = int.Parse(UserXML["followers_count"].InnerText);
        //        Favote = int.Parse(UserXML["favourites_count"].InnerText);
        //        Status = int.Parse(UserXML["statuses_count"].InnerText);
        //        Admission = DateTime.ParseExact(UserXML["created_at"].InnerText,
        //                                        "ddd MMM dd HH:mm:ss K yyyy",
        //                                        System.Globalization.DateTimeFormatInfo.InvariantInfo);
        //    }
        //}
        public User(string UserJsonString)
        {
            var user = DynamicJson.Parse(UserJsonString);

            UserID = (ulong)user["id"];
            Name = user["name"];
            ScreenName = user["screen_name"];
            Location = user["location"];
            Description = user["description"];
            if (user["profile_image_url"] != null)
            {
                WebClient wc = new WebClient();
                Stream stream = wc.OpenRead(user["profile_image_url"]);
                ProfileImage = Image.FromStream(stream);
                stream.Dispose();
                wc.Dispose();
            }
            if (user["url"] != null)
                Uri = new Uri(user["url"]);
            Protected = user["protected"];
            Verified = user["verified"];
            Friend = (int)user["friends_count"];
            Follower = (int)user["followers_count"];
            Favote = (int)user["favourites_count"];
            Status = (int)user["statuses_count"];
            Admission = DateTime.ParseExact(user["created_at"],
                                                "ddd MMM dd HH:mm:ss K yyyy",
                                                System.Globalization.DateTimeFormatInfo.InvariantInfo);

            //JsonContract.UserJsonContract jsonobject = JsonConvert.DeserializeObject<JsonContract.UserJsonContract>(UserJsonString);

            //UserID = jsonobject.ID;
            //Name = jsonobject.Name;
            //ScreenName = jsonobject.ScreenName;
            //Location = jsonobject.Location;
            //Description = jsonobject.Description;
            //if (jsonobject.ProfileImageUrl != null)
            //{
            //    WebClient wc = new WebClient();
            //    Stream stream = wc.OpenRead(jsonobject.ProfileImageUrl);
            //    ProfileImage = Image.FromStream(stream);
            //    stream.Dispose();
            //    wc.Dispose();
            //}
            //if (jsonobject.URL != null)
            //    Uri = new Uri(jsonobject.URL);
            //Protected = jsonobject.Protected;
            //Verified = jsonobject.Verified;
            //Friend = jsonobject.Friend;
            //Follower = jsonobject.Follower;
            //Favote = jsonobject.Favoute;
            //Status = jsonobject.StatusCount;
            //Admission = DateTime.ParseExact(jsonobject.CreatedAt,
            //                                    "ddd MMM dd HH:mm:ss K yyyy",
            //                                    System.Globalization.DateTimeFormatInfo.InvariantInfo);

            //DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(JsonContract.UserJsonContract));
            //Stream st = new MemoryStream(Encoding.UTF8.GetBytes(UserJsonString));
            //JsonContract.UserJsonContract jsuw = (JsonContract.UserJsonContract)serializer.ReadObject(st);

            //UserID = (ulong)jsuw.ID;
            //Name = jsuw.Name;
            //ScreenName = jsuw.ScreenName;
            //Location = jsuw.Location;
            //Description = jsuw.Description;
            //if (jsuw.ProfileImageUrl != "")
            //{
            //    WebClient wc = new WebClient();
            //    Stream stream = wc.OpenRead(jsuw.ProfileImageUrl);
            //    ProfileImage = Image.FromStream(stream);
            //    stream.Dispose();
            //    wc.Dispose();
            //}
            //if (jsuw.URL != "")
            //    Uri = new Uri(jsuw.URL);
            //Protected = jsuw.Protected;
            //Verified = jsuw.Verified;
            //Friend = jsuw.Friend;
            //Follower = jsuw.Follower;
            //Favote = jsuw.Favoute;
            //Status = jsuw.StatusCount;
            //Admission = DateTime.ParseExact(jsuw.CreatedAt,
            //                                    "ddd MMM dd HH:mm:ss K yyyy",
            //                                    System.Globalization.DateTimeFormatInfo.InvariantInfo);
        }
        //public User(JsonContract.UserJsonContract user)
        //{
        //    UserID = user.ID;
        //    Name = user.Name;
        //    ScreenName = user.ScreenName;
        //    Location = user.Location;
        //    Description = user.Description;
        //    if (user.ProfileImageUrl != null)
        //    {
        //        WebClient wc = new WebClient();
        //        Stream stream = wc.OpenRead(user.ProfileImageUrl);
        //        ProfileImage = Image.FromStream(stream);
        //        stream.Dispose();
        //        wc.Dispose();
        //    }
        //    if (user.URL != null)
        //        Uri = new Uri(user.URL);
        //    Protected = user.Protected;
        //    Verified = user.Verified;
        //    Friend = user.Friend;
        //    Follower = user.Follower;
        //    Favote = user.Favoute;
        //    Status = user.StatusCount;
        //    Admission = DateTime.ParseExact(user.CreatedAt,
        //                                        "ddd MMM dd HH:mm:ss K yyyy",
        //                                        System.Globalization.DateTimeFormatInfo.InvariantInfo);
        //}
    }
    public class UserList
    {
        public string Name, FullName, Description, Mode, Slug;
        public bool Following;
        public int MenberCount, SubScriberCount;
        public UInt64 ID, CreateUserID;
        public DateTime CreatedAt;
        public Uri URL;

        public UserList()
        {
        }
        public UserList(string JsonText)
        {
            var json = DynamicJson.Parse(JsonText);
            Name = json["name"];
            FullName = json["full_name"];
            Following = json["following"];
            MenberCount = (int)json["member_count"];
            Description = json["description"];
            Mode = json["mode"];
            ID = (UInt64)json["id"];
            SubScriberCount = (int)json["subscriber_count"];
            CreatedAt = DateTime.ParseExact(json["created_at"],
                                               "ddd MMM dd HH:mm:ss K yyyy",
                                               System.Globalization.DateTimeFormatInfo.InvariantInfo);
            URL = new Uri("http://twitter.com/#!/" + json["uri"]);
            CreateUserID = (UInt64)json["user"]["id"];
        }
        /// <summary>
        /// 未完成
        /// </summary>
        /// <param name="ListXML"></param>
        public UserList(XmlNode ListXML)
        {
        }
    }
    public struct TweetAUser
    {
        public Tweet Tweet;
        public User User;
    }
    public struct ListAUser
    {
        public UserList List;
        public User User;
    }
    public struct UserAUser
    {
        public User Target;
        public User Source;
    }
    public struct Target
    {
        /*     Eventの中身、またはEventNumが以下の場合のDataの型
         *      Event          EventNum    Objectの型          内容
         *      Follow         1           Twitry.User         Target:フォローされた人 Source:フォローした人
         *      UserUpdate     2           Twitry.User         自分自身のデータ(プロフィールデータ)
         *      Block          3           Twitry.UserAUser    Target:ブロックされた人 Source:ブロックした人
         *      UnBlock        4           Twitry.UserAUser    Target:ブロックを解除された人 Source:ブロック解除した人
         *      Favorite       5           Twitry.TweetAUser   User:ふぁぼった人 Tweet:ふぁぼったTweet
         *      UnFavorite     6           Twitry.TweetAUser   User:解除した人   Tweet:解除されたTweet
         *      ListCreate     7           Twitry.List         リストのデータ
         *      ListDestroyed  8           Twitry.List         消されたリストのデータ
         *      ListMemberAdd  9           Twitry.ListAUser    User:追加した人       List:追加されたリストのデータ
         *      ListMemberDel  10          Twitry.ListAUser    User:外された人       List:外された後のリストのデータ
         *      ListUpdate     11          Twitry.ListAUser    User:更新した人 　　　List:更新されたリストのデータ
         *      ListUserSub    12          Twitry.ListAUser    User:フォローした人   List:フォローしたリストのデータ
         *      ListUserUnSub  13          Twitry.ListAUser    User:解除した人　　　 List:解除したリストのデータ
         *      
         *     Time ・・・ イベントが発生した時間
        */
        public string Event;
        public int EventNum;
        public object Object;
        public DateTime Time;
    }

    //namespace JsonContract
    //{
    //    /*
    //     *  Jsonデシリアライズ(解読)用の型みたいな奴。
    //     *  もう書きたくない。
    //     */
    //    [Serializable]
    //    public class TweetJsonContract
    //    {
    //        [JsonProperty("created_at")]
    //        public string CreateAt { get; set; }
    //        [JsonProperty("id")]
    //        public UInt64 TweetID { get; set; }
    //        [JsonProperty("id_str")]
    //        public string TweetIDString { get; set; }
    //        [JsonProperty("text")]
    //        public string Status { get; set; }
    //        [JsonProperty("source")]
    //        public string Source { get; set; }
    //        [JsonProperty("truncated")]
    //        public bool Truncated { get; set; }
    //        [JsonProperty("in_reply_to_status_id")]
    //        public string ReplyStatusID { get; set; }
    //        [JsonProperty("in_reply_to_status_id_str")]
    //        public string ReplyStatusIDString { get; set; }
    //        [JsonProperty("in_reply_to_user_id")]
    //        public string ReplyUserID { get; set; }
    //        [JsonProperty("in_reply_to_user_id_str")]
    //        public string ReplyUserIDString { get; set; }
    //        [JsonProperty("in_reply_to_screen_name")]
    //        public string ReplyScreenName { get; set; }
    //        [JsonProperty("user")]
    //        public UserJsonContract User { get; set; }
    //        [JsonProperty("geo")]
    //        public CoordinatesJsonContract Geo { get; set; }
    //        [JsonProperty("coordinates")]
    //        public CoordinatesJsonContract Coordinates { get; set; }
    //        [JsonProperty("place")]
    //        public PlaceJsonContract Place { get; set; }
    //        [JsonProperty("contributors")]
    //        public string Contributors { get; set; }
    //        [JsonProperty("retweeted_status")]
    //        public TweetJsonContract RetweetStatus { get; set; }
    //        [JsonProperty("retweet_count")]
    //        public int RetweetCount { get; set; }
    //        [JsonProperty("favorited")]
    //        public bool Favorited { get; set; }
    //        [JsonProperty("retweeted")]
    //        public bool Retweeted { get; set; }
    //    }
    //    [Serializable]
    //    public class UserJsonContract
    //    {
    //        [JsonProperty("id")]
    //        public UInt64 ID { get; set; }
    //        [JsonProperty("id_str")]
    //        public string IDString { get; set; }
    //        [JsonProperty("name")]
    //        public string Name { get; set; }
    //        [JsonProperty("screen_name")]
    //        public string ScreenName { get; set; }
    //        [JsonProperty("location")]
    //        public string Location { get; set; }
    //        [JsonProperty("url")]
    //        public string URL { get; set; }
    //        [JsonProperty("description")]
    //        public string Description { get; set; }
    //        [JsonProperty("protected")]
    //        public bool Protected { get; set; }
    //        [JsonProperty("followers_count")]
    //        public int Follower { get; set; }
    //        [JsonProperty("friends_count")]
    //        public int Friend { get; set; }
    //        [JsonProperty("listed_count")]
    //        public int List { get; set; }
    //        [JsonProperty("created_at")]
    //        public string CreatedAt { get; set; }
    //        [JsonProperty("favourites_count")]
    //        public int Favoute { get; set; }
    //        [JsonProperty("utc_offset")]
    //        public string UTCOffset { get; set; }
    //        [JsonProperty("time_zone")]
    //        public string TimeZone { get; set; }
    //        [JsonProperty("geo_enabled")]
    //        public bool GEOEnabled { get; set; }
    //        [JsonProperty("verified")]
    //        public bool Verified { get; set; }
    //        [JsonProperty("statuses_count")]
    //        public int StatusCount { get; set; }
    //        [JsonProperty("lang")]
    //        public string Language { get; set; }
    //        //[JsonProperty("status")]
    //        //public TweetJsonContract Status { get; set; }
    //        [JsonProperty("contributors_enabled")]
    //        public bool Contributors { get; set; }
    //        [JsonProperty("is_translator")]
    //        public bool Translator { get; set; }
    //        [JsonProperty("profile_background_color")]
    //        public string ProfileBackgroundColor { get; set; }
    //        [JsonProperty("profile_background_image_url")]
    //        public string ProfileBackgroundImageURL { get; set; }
    //        [JsonProperty("profile_background_image_url_https")]
    //        public string ProfileBackgroundImageURLHttps { get; set; }
    //        [JsonProperty("profile_background_tile")]
    //        public bool ProfileBackgroundTile { get; set; }
    //        [JsonProperty("profile_image_url")]
    //        public string ProfileImageUrl { get; set; }
    //        [JsonProperty("profile_image_url_https")]
    //        public string ProfileImageUrlHttps { get; set; }
    //        [JsonProperty("profile_link_color")]
    //        public string ProfileLinkColor { get; set; }
    //        [JsonProperty("profile_sidebar_border_color")]
    //        public string ProfileSidebarBorderColor { get; set; }
    //        [JsonProperty("profile_sidebar_fill_color")]
    //        public string ProfileSidebarFillColor { get; set; }
    //        [JsonProperty("profile_text_color")]
    //        public string ProfileTextColor { get; set; }
    //        [JsonProperty("profile_use_background_image")]
    //        public bool ProfileUseBackgroundImage { get; set; }
    //        [JsonProperty("show_all_inline_media")]
    //        public bool ShowAllInlineMedia { get; set; }
    //        [JsonProperty("default_profile")]
    //        public bool DefaultProfile { get; set; }
    //        [JsonProperty("default_profile_image")]
    //        public bool DefaultProfileImage { get; set; }
    //        [JsonProperty("following")]
    //        public string Following { get; set; }
    //        [JsonProperty("follow_request_sent")]
    //        public string FollowRequestSent { get; set; }
    //        [JsonProperty("notifications")]
    //        public string Notifications { get; set; }
    //    }
    //    [Serializable]
    //    public class FriendJsonContract
    //    {
    //        [JsonProperty("friends")]
    //        public UInt64[] FriendsID { get; set; }
    //    }
    //    [Serializable]
    //    public class CoordinatesJsonContract
    //    {
    //        [JsonProperty("type")]
    //        public string Type { get; set; }
    //        [JsonProperty("coordinates")]
    //        public float[] Coordinates { get; set; }
    //    }
    //    [Serializable]
    //    public class PlaceJsonContract
    //    {
    //        [JsonProperty("countory")]
    //        public string Countory { get; set; }
    //        [JsonProperty("place_type")]
    //        public string PlaceType { get; set; }
    //        [JsonProperty("url")]
    //        public string URL { get; set; }
    //        //[JsonProperty("attributes")]
    //        //public string[] Attributes { get; set; }
    //        [JsonProperty("full_name")]
    //        public string FullName { get; set; }
    //        [JsonProperty("name")]
    //        public string Name { get; set; }
    //        [JsonProperty("country_code")]
    //        public string CountryCode { get; set; }
    //        [JsonProperty("id")]
    //        public string ID { get; set; }
    //        [JsonProperty("coordinates")]
    //        public BoundingBoxJsonContract Coordinates { get; set; }

    //    }
    //    [Serializable]
    //    public class BoundingBoxJsonContract
    //    {
    //        [JsonProperty("type")]
    //        public string Type { get; set; }
    //        [JsonProperty("coordinates")]
    //        public float[] Point { get; set; }
    //    }
    //    [Serializable]
    //    public class DeleteJsonContract
    //    {
    //        [JsonProperty("delete", Order = 0)]
    //        public StatusJsonContract status { get; set; }
    //    }
    //    public class StatusJsonContract
    //    {
    //        [JsonProperty("status")]
    //        TweetJsonContract DeleteStatus { get; set; }
    //    }
    //}

    //namespace JsonContract
    //{
    //    /*
    //     *  Jsonデシリアライズ(解読)用の型みたいな奴。
    //     *  もう書きたくない。
    //     */

    //    [System.Runtime.Serialization.DataContract()]
    //    class TweetJsonContract
    //    {
    //        [JsonProperty("create_at")]
    //        public string CreateAt { get; set; }
    //        [JsonProperty("id")]
    //        public UInt64 TweetID { get; set; }
    //        [JsonProperty("id_str")]
    //        public string TweetIDString { get; set; }
    //        [JsonProperty("text")]
    //        public string Status { get; set; }
    //        [JsonProperty("source")]
    //        public string Source { get; set; }
    //        [JsonProperty("truncated")]
    //        public bool Truncated { get; set; }
    //        [JsonProperty("in_reply_to_status_id")]
    //        public string ReplyStatusID { get; set; }
    //        [JsonProperty("in_reply_to_status_id_str")]
    //        public string ReplyStatusIDString { get; set; }
    //        [JsonProperty("in_reply_to_user_id")]
    //        public string ReplyUserID { get; set; }
    //        [JsonProperty("in_reply_to_user_id_str")]
    //        public string ReplyUserIDString { get; set; }
    //        [JsonProperty("in_reply_to_screen_name")]
    //        public string ReplyScreenName { get; set; }
    //        [JsonProperty("user")]
    //        public string User { get; set; }
    //        [JsonProperty("geo")]
    //        public string Geo { get; set; }
    //        [JsonProperty("coordinates")]
    //        public string Coordinates { get; set; }
    //        [JsonProperty("place")]
    //        public string Place { get; set; }
    //        [JsonProperty("contributors")]
    //        public string Contributors { get; set; }
    //        [JsonProperty("retweeted_status")]
    //        public string RetweetStatus;
    //        [JsonProperty("retweet_count")]
    //        public int RetweetCount { get; set; }
    //        [JsonProperty("favorited")]
    //        public bool Favorited { get; set; }
    //        [JsonProperty("retweeted")]
    //        public bool Retweeted { get; set; }
    //    }

    //    [System.Runtime.Serialization.DataContract()]
    //    class UserJsonContract
    //    {
    //        [JsonProperty("id")]
    //        public int ID { get; set; }
    //        [JsonProperty("id_str")]
    //        public string IDString { get; set; }
    //        [JsonProperty("name")]
    //        public string Name { get; set; }
    //        [JsonProperty("screen_name")]
    //        public string ScreenName { get; set; }
    //        [JsonProperty("location")]
    //        public string Location { get; set; }
    //        [JsonProperty("url")]
    //        public string URL { get; set; }
    //        [JsonProperty("description")]
    //        public string Description { get; set; }
    //        [JsonProperty("protected")]
    //        public bool Protected { get; set; }
    //        [JsonProperty("followers_count")]
    //        public int Follower { get; set; }
    //        [JsonProperty("friends_count")]
    //        public int Friend { get; set; }
    //        [JsonProperty("listed_count")]
    //        public int List { get; set; }
    //        [JsonProperty("created_at")]
    //        public string CreatedAt { get; set; }
    //        [JsonProperty("favourites_count")]
    //        public int Favoute { get; set; }
    //        [JsonProperty("utc_offset")]
    //        public int UTCOffset { get; set; }
    //        [JsonProperty("time_zone")]
    //        public string TimeZone { get; set; }
    //        [JsonProperty("geo_enabled")]
    //        public bool GEOEnabled { get; set; }
    //        [JsonProperty("verified")]
    //        public bool Verified { get; set; }
    //        [JsonProperty("statuses_count")]
    //        public int StatusCount { get; set; }
    //        [JsonProperty("lang")]
    //        public string Language { get; set; }
    //        [JsonProperty("status")]
    //        public TweetJsonContract Status { get; set; }
    //        [JsonProperty("contributors_enabled")]
    //        public bool Contributors { get; set; }
    //        [JsonProperty("is_translator")]
    //        public bool Translator { get; set; }
    //        [JsonProperty("profile_background_color")]
    //        public string ProfileBackgroundColor { get; set; }
    //        [JsonProperty("profile_background_image_url")]
    //        public string ProfileBackgroundImageURL { get; set; }
    //        [JsonProperty("profile_background_image_url_https")]
    //        public string ProfileBackgroundImageURLHttps { get; set; }
    //        [JsonProperty("profile_background_tile")]
    //        public bool ProfileBackgroundTile { get; set; }
    //        [JsonProperty("profile_image_url")]
    //        public string ProfileImageUrl { get; set; }
    //        [JsonProperty("profile_image_url_https")]
    //        public string ProfileImageUrlHttps { get; set; }
    //        [JsonProperty("profile_link_color")]
    //        public string ProfileLinkColor { get; set; }
    //        [JsonProperty("profile_sidebar_border_color")]
    //        public string ProfileSidebarBorderColor { get; set; }
    //        [JsonProperty("profile_sidebar_fill_color")]
    //        public string ProfileSidebarFillColor { get; set; }
    //        [JsonProperty("profile_text_color")]
    //        public string ProfileTextColor { get; set; }
    //        [JsonProperty("profile_use_background_image")]
    //        public bool ProfileUseBackgroundImage { get; set; }
    //        [JsonProperty("show_all_inline_media")]
    //        public bool ShowAllInlineMedia { get; set; }
    //        [JsonProperty("default_profile")]
    //        public bool DefaultProfile { get; set; }
    //        [JsonProperty("default_profile_image")]
    //        public bool DefaultProfileImage { get; set; }
    //        [JsonProperty("following")]
    //        public bool Following { get; set; }
    //        [JsonProperty("follow_request_sent")]
    //        public bool FollowRequestSent { get; set; }
    //        [JsonProperty("notifications")]
    //        public bool Notifications { get; set; }
    //    }
    //}
    public delegate void AddTweetHandler(TweetAUser tw);
    public delegate void FriendHandler(List<ulong> UserIDs);
    public delegate void DeleteTweetHandler(UInt64 TweetID);
    public delegate void TargetHandler(Target Data);
}


/*--------------------------------------------------------------------------
* DynamicJson
* ver 1.2.0.0 (May. 21th, 2010)
*
* created and maintained by neuecc <ils@neue.cc>
* licensed under Microsoft Public License(Ms-PL)
* http://neue.cc/
* http://dynamicjson.codeplex.com/
*--------------------------------------------------------------------------*/
namespace Codeplex.Data
{
    public class DynamicJson : DynamicObject
    {
        private enum JsonType
        {
            @string, number, boolean, @object, array, @null
        }

        // public static methods

        /// <summary>from JsonSring to DynamicJson</summary>
        public static dynamic Parse(string json)
        {
            return Parse(json, Encoding.Unicode);
        }

        /// <summary>from JsonSring to DynamicJson</summary>
        public static dynamic Parse(string json, Encoding encoding)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(encoding.GetBytes(json), XmlDictionaryReaderQuotas.Max))
            {
                return ToValue(XElement.Load(reader));
            }
        }

        /// <summary>from JsonSringStream to DynamicJson</summary>
        public static dynamic Parse(Stream stream)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, XmlDictionaryReaderQuotas.Max))
            {
                return ToValue(XElement.Load(reader));
            }
        }

        /// <summary>from JsonSringStream to DynamicJson</summary>
        public static dynamic Parse(Stream stream, Encoding encoding)
        {
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(stream, encoding, XmlDictionaryReaderQuotas.Max, _ => { }))
            {
                return ToValue(XElement.Load(reader));
            }
        }

        /// <summary>create JsonSring from primitive or IEnumerable or Object({public property name:property value})</summary>
        public static string Serialize(object obj)
        {
            return CreateJsonString(new XStreamingElement("root", CreateTypeAttr(GetJsonType(obj)), CreateJsonNode(obj)));
        }

        // private static methods

        private static dynamic ToValue(XElement element)
        {
            var type = (JsonType)Enum.Parse(typeof(JsonType), element.Attribute("type").Value);
            switch (type)
            {
                case JsonType.boolean:
                    return (bool)element;
                case JsonType.number:
                    return (double)element;
                case JsonType.@string:
                    return (string)element;
                case JsonType.@object:
                case JsonType.array:
                    return new DynamicJson(element, type);
                case JsonType.@null:
                default:
                    return null;
            }
        }

        private static JsonType GetJsonType(object obj)
        {
            if (obj == null) return JsonType.@null;

            switch (Type.GetTypeCode(obj.GetType()))
            {
                case TypeCode.Boolean:
                    return JsonType.boolean;
                case TypeCode.String:
                case TypeCode.Char:
                case TypeCode.DateTime:
                    return JsonType.@string;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.SByte:
                case TypeCode.Byte:
                    return JsonType.number;
                case TypeCode.Object:
                    return (obj is IEnumerable) ? JsonType.array : JsonType.@object;
                case TypeCode.DBNull:
                case TypeCode.Empty:
                default:
                    return JsonType.@null;
            }
        }

        private static XAttribute CreateTypeAttr(JsonType type)
        {
            return new XAttribute("type", type.ToString());
        }

        private static object CreateJsonNode(object obj)
        {
            var type = GetJsonType(obj);
            switch (type)
            {
                case JsonType.@string:
                case JsonType.number:
                    return obj;
                case JsonType.boolean:
                    return obj.ToString().ToLower();
                case JsonType.@object:
                    return CreateXObject(obj);
                case JsonType.array:
                    return CreateXArray(obj as IEnumerable);
                case JsonType.@null:
                default:
                    return null;
            }
        }

        private static IEnumerable<XStreamingElement> CreateXArray<T>(T obj) where T : IEnumerable
        {
            return obj.Cast<object>()
                .Select(o => new XStreamingElement("item", CreateTypeAttr(GetJsonType(o)), CreateJsonNode(o)));
        }

        private static IEnumerable<XStreamingElement> CreateXObject(object obj)
        {
            return obj.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(pi => new { Name = pi.Name, Value = pi.GetValue(obj, null) })
                .Select(a => new XStreamingElement(a.Name, CreateTypeAttr(GetJsonType(a.Value)), CreateJsonNode(a.Value)));
        }

        private static string CreateJsonString(XStreamingElement element)
        {
            using (var ms = new MemoryStream())
            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.Unicode))
            {
                element.WriteTo(writer);
                writer.Flush();
                return Encoding.Unicode.GetString(ms.ToArray());
            }
        }

        // dynamic structure represents JavaScript Object/Array

        readonly XElement xml;
        readonly JsonType jsonType;

        /// <summary>create blank JSObject</summary>
        public DynamicJson()
        {
            xml = new XElement("root", CreateTypeAttr(JsonType.@object));
            jsonType = JsonType.@object;
        }

        private DynamicJson(XElement element, JsonType type)
        {
            Debug.Assert(type == JsonType.array || type == JsonType.@object);

            xml = element;
            jsonType = type;
        }

        public bool IsObject { get { return jsonType == JsonType.@object; } }

        public bool IsArray { get { return jsonType == JsonType.array; } }

        /// <summary>has property or not</summary>
        public bool IsDefined(string name)
        {
            return IsObject && (xml.Element(name) != null);
        }

        /// <summary>has property or not</summary>
        public bool IsDefined(int index)
        {
            return IsArray && (xml.Elements().ElementAtOrDefault(index) != null);
        }

        /// <summary>delete property</summary>
        public bool Delete(string name)
        {
            var elem = xml.Element(name);
            if (elem != null)
            {
                elem.Remove();
                return true;
            }
            else return false;
        }

        /// <summary>delete property</summary>
        public bool Delete(int index)
        {
            var elem = xml.Elements().ElementAtOrDefault(index);
            if (elem != null)
            {
                elem.Remove();
                return true;
            }
            else return false;
        }

        /// <summary>mapping to Array or Class by Public PropertyName</summary>
        public T Deserialize<T>()
        {
            return (T)Deserialize(typeof(T));
        }

        private object Deserialize(Type type)
        {
            return (IsArray) ? DeserializeArray(type) : DeserializeObject(type);
        }

        private dynamic DeserializeValue(XElement element, Type elementType)
        {
            var value = ToValue(element);
            if (value is DynamicJson)
            {
                value = ((DynamicJson)value).Deserialize(elementType);
            }
            return Convert.ChangeType(value, elementType);
        }

        private object DeserializeObject(Type targetType)
        {
            var result = Activator.CreateInstance(targetType);
            var dict = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(pi => pi.Name, pi => pi);
            foreach (var item in xml.Elements())
            {
                PropertyInfo propertyInfo;
                if (!dict.TryGetValue(item.Name.LocalName, out propertyInfo)) continue;
                var value = DeserializeValue(item, propertyInfo.PropertyType);
                propertyInfo.SetValue(result, value, null);
            }
            return result;
        }

        private object DeserializeArray(Type targetType)
        {
            if (targetType.IsArray) // Foo[]
            {
                var elemType = targetType.GetElementType();
                dynamic array = Array.CreateInstance(elemType, xml.Elements().Count());
                var index = 0;
                foreach (var item in xml.Elements())
                {
                    array[index++] = DeserializeValue(item, elemType);
                }
                return array;
            }
            else // List<Foo>
            {
                var elemType = targetType.GetGenericArguments()[0];
                dynamic list = Activator.CreateInstance(targetType);
                foreach (var item in xml.Elements())
                {
                    list.Add(DeserializeValue(item, elemType));
                }
                return list;
            }
        }

        // Delete
        public override bool TryInvoke(InvokeBinder binder, object[] args, out object result)
        {
            result = (IsArray)
                ? Delete((int)args[0])
                : Delete((string)args[0]);
            return true;
        }

        // IsDefined, if has args then TryGetMember
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            if (args.Length > 0)
            {
                result = null;
                return false;
            }

            result = IsDefined(binder.Name);
            return true;
        }

        // Deserialize or foreach(IEnumerable)
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.Type == typeof(IEnumerable) || binder.Type == typeof(object[]))
            {
                var ie = (IsArray)
                    ? xml.Elements().Select(x => ToValue(x))
                    : xml.Elements().Select(x => (dynamic)new KeyValuePair<string, object>(x.Name.LocalName, ToValue(x)));
                result = (binder.Type == typeof(object[])) ? ie.ToArray() : ie;
            }
            else
            {
                result = Deserialize(binder.Type);
            }
            return true;
        }

        private bool TryGet(XElement element, out object result)
        {
            if (element == null)
            {
                result = null;
                return false;
            }

            result = ToValue(element);
            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            return (IsArray)
                ? TryGet(xml.Elements().ElementAtOrDefault((int)indexes[0]), out result)
                : TryGet(xml.Element((string)indexes[0]), out result);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return (IsArray)
                ? TryGet(xml.Elements().ElementAtOrDefault(int.Parse(binder.Name)), out result)
                : TryGet(xml.Element(binder.Name), out result);
        }

        private bool TrySet(string name, object value)
        {
            var type = GetJsonType(value);
            var element = xml.Element(name);
            if (element == null)
            {
                xml.Add(new XElement(name, CreateTypeAttr(type), CreateJsonNode(value)));
            }
            else
            {
                element.Attribute("type").Value = type.ToString();
                element.ReplaceNodes(CreateJsonNode(value));
            }

            return true;
        }

        private bool TrySet(int index, object value)
        {
            var type = GetJsonType(value);
            var e = xml.Elements().ElementAtOrDefault(index);
            if (e == null)
            {
                xml.Add(new XElement("item", CreateTypeAttr(type), CreateJsonNode(value)));
            }
            else
            {
                e.Attribute("type").Value = type.ToString();
                e.ReplaceNodes(CreateJsonNode(value));
            }

            return true;
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            return (IsArray)
                ? TrySet((int)indexes[0], value)
                : TrySet((string)indexes[0], value);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            return (IsArray)
                ? TrySet(int.Parse(binder.Name), value)
                : TrySet(binder.Name, value);
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return (IsArray)
                ? xml.Elements().Select((x, i) => i.ToString())
                : xml.Elements().Select(x => x.Name.LocalName);
        }

        /// <summary>Serialize to JsonString</summary>
        public override string ToString()
        {
            // <foo type="null"></foo> is can't serialize. replace to <foo type="null" />
            foreach (var elem in xml.Descendants().Where(x => x.Attribute("type").Value == "null"))
            {
                elem.RemoveNodes();
            }
            return CreateJsonString(new XStreamingElement("root", CreateTypeAttr(jsonType), xml.Elements()));
        }
    }
}