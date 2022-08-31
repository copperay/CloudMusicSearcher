using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CloudMusicSearcher
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>

    // 歌曲信息类
    public class Song
    {
        public int id { get; set; }
        public string title { get; set; }
        public List<string> artists { get; set; }
        public string album { get; set; }
        public string artistsOut
        {
            get
            {
                string retn = "";
                foreach (var item in artists)
                {
                    retn += " ";
                    retn += item;
                    retn += "; ";
                }
                return retn.Trim(';', ' ');
            }
            private set { }
        }

        public Song(JToken id, JToken title, JToken artists, JToken album)
        {
            this.artists = new List<string>();
            this.id = (int)id;
            this.title = (string)title;
            foreach (var item in artists.Children())
            {
                this.artists.Add((string)item["name"]);
            }
            this.album = (string)album["name"];
        }
    }

    // 网络搜索结果类
    public class WebSearchSongsResult
    {
        public List<Song> songs;
        public int stateCode;

        public WebSearchSongsResult()
        {
            stateCode = 0;
            songs = new List<Song>();
        }
    }

    // 网络查询结果类
    public class WebQueryCoverResult
    {
        public string picUrl;
        public int stateCode;
    }

    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        const int WEB_SEARCH_LIMIT = 6; // 搜索页每页项数

        int webSearchOffset = 0; // 搜索页偏移值

        const string COPYRIGHT_INFO =
@"- 版权所有：Copper；

- 本软件的搜索结果系调用网易云音乐官方API所得；

- 作者出于学习需要，写就本软件，且已开源，不参与任何商业营利活动（开源地址：https://github.com/copperay/CloudMusicSearcher）；

- 若本软件侵犯了您的权利，烦请即刻联系作者！


- All rights reserved: Copper.

- The search results of this software are obtained form official APIs of NetEase Cloud Music.

- Author wrote this software out of the need of learning. It is an OSS and does not associate with any commercial activities. (Source code available at: https://github.com/copperay/CloudMusicSearcher)

- If this software violates your rights, please contact the author immediately!";

        // “搜索”按钮
        private void WebSearchButton_Click(object sender, RoutedEventArgs e)
        {
            // 创建URL字符串
            string WebSearchURL = "http://music.163.com/api/search/get/web?s=" + webSearchText + "&type=1&offset=" + webSearchOffset.ToString() + "&limit=" + WEB_SEARCH_LIMIT.ToString();

            // 发送request并求得response
            HttpWebRequest webSearchReq = (HttpWebRequest)WebRequest.CreateHttp(WebSearchURL);
            HttpWebResponse webSearchResp = (HttpWebResponse)webSearchReq.GetResponse();
            StreamReader webSearchRespSR = new StreamReader(webSearchResp.GetResponseStream());

            // 处理返回的Json数据
            WebSearchSongsResult webSearchSongsResult = new WebSearchSongsResult();
            JObject webSearchSongsJson = JObject.Parse(webSearchRespSR.ReadToEnd());
            webSearchSongsResult.stateCode = (int)webSearchSongsJson["code"];

            if (webSearchSongsResult.stateCode == 200) // 若正常返回
            {
                // 反序列化Json中的歌曲信息
                foreach (var item in webSearchSongsJson["result"]["songs"].Children())
                {
                    webSearchSongsResult.songs.Add(new Song(
                        item["id"],
                        item["name"],
                        item["artists"],
                        item["album"]));
                }
            }
            else // 若非正常返回
            {
                MessageBox.Show("服务器开小差，请稍后再试！");
                return;
            }

            // 输出搜索结果到ListView
            webSearchResultList.ItemsSource = webSearchSongsResult.songs;
        }

        // “清空”按钮
        private void clearDisplayButton_Click(object sender, RoutedEventArgs e)
        {
            webSearchResultList.ItemsSource = null;
        }

        // “拷贝ID”按钮
        private void CopyIdButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(((Song)webSearchResultList.SelectedItem).id.ToString());
            MessageBox.Show("拷贝成功!");
        }

        // 实现点选歌曲时切换专辑封面
        private void webSearchResultList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (webSearchResultList.IsMouseCaptured == false)
            {
                return;
            }
            // 获取选中歌曲的ID
            string songId = ((Song)webSearchResultList.SelectedItem).id.ToString();

            // 创建URL字符串
            string webQueryCoverURL = "http://music.163.com/api/song/detail/?ids=[" + songId + "]";

            // 发送request并求得response
            HttpWebRequest webQueryCoverReq = (HttpWebRequest)WebRequest.CreateHttp(webQueryCoverURL);
            HttpWebResponse webQueryCoverResp = (HttpWebResponse)webQueryCoverReq.GetResponse();
            StreamReader webQueryCoverRespSR = new StreamReader(webQueryCoverResp.GetResponseStream());

            // 处理返回的Json数据
            WebQueryCoverResult webQueryCoverResult = new WebQueryCoverResult();
            JObject webQueryCoverJson = JObject.Parse(webQueryCoverRespSR.ReadToEnd());
            webQueryCoverResult.stateCode = (int)webQueryCoverJson["code"];

            if (webQueryCoverResult.stateCode == 200) // 若正常返回
            {
                // 反序列化Json并将图片输出到Image控件
                albumCoverImage.Source = new BitmapImage(new System.Uri(webQueryCoverJson["songs"][0]["album"]["picUrl"].ToString()));
            }
            else // 若非正常返回
            {
                MessageBox.Show("封面获取失败，请稍后再试！");
                return;
            }
        }

        // 实现点击“作者”超链接跳转
        private void authorHyperlink_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(authorHyperlink.NavigateUri.ToString());
        }

        // “下一页”按钮
        private void nextPageButton_Click(object sender, RoutedEventArgs e)
        {
            webSearchOffset += WEB_SEARCH_LIMIT;
            WebSearchButton_Click(sender, e);
            RefreshPrevPageButtonState();
        }

        // 实现刷新“上一页”按钮的可用状态
        private void RefreshPrevPageButtonState()
        {
            if (webSearchOffset != 0)
            {
                prevPageButton.IsEnabled = true;
            }
            else
            {
                prevPageButton.IsEnabled = false;
            }
        }

        // “上一页”按钮
        private void prevPageButton_Click(object sender, RoutedEventArgs e)
        {
            webSearchOffset -= WEB_SEARCH_LIMIT;
            WebSearchButton_Click(sender, e);
            RefreshPrevPageButtonState();
        }

        private void copyrightInfoHyperlink_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(COPYRIGHT_INFO, "© 版权信息", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

/*

http://music.163.com/api/song/detail/?id=346089&ids=[346089]

{
    "songs": [
        {
            "name": "海阔天空",
            "id": 346089,
            "position": 41,
            "alias": [],
            "status": 0,
            "fee": 8,
            "copyrightId": 7002,
            "disc": "03",
            "no": 6,
            "artists": [
                {
                    "name": "Beyond",
                    "id": 11127,
                    "picId": 0,
                    "img1v1Id": 0,
                    "briefDesc": "",
                    "picUrl": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                    "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                    "albumSize": 0,
                    "alias": [],
                    "trans": "",
                    "musicSize": 0,
                    "topicPerson": 0
                }
            ],
            "album": {
                "name": "Beyond 25th Anniversary",
                "id": 34110,
                "type": "精选集",
                "size": 48,
                "picId": 109951165796417308,
                "blurPicUrl": "http://p2.music.126.net/zZtUDuWk6qIe3ezMt4UMjg==/109951165796417308.jpg",
                "companyId": 0,
                "pic": 109951165796417308,
                "picUrl": "http://p2.music.126.net/zZtUDuWk6qIe3ezMt4UMjg==/109951165796417308.jpg",
                "publishTime": 1205164800000,
                "description": "",
                "tags": "",
                "company": "新艺宝",
                "briefDesc": "",
                "artist": {
                    "name": "",
                    "id": 0,
                    "picId": 0,
                    "img1v1Id": 0,
                    "briefDesc": "",
                    "picUrl": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                    "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                    "albumSize": 0,
                    "alias": [],
                    "trans": "",
                    "musicSize": 0,
                    "topicPerson": 0
                },
                "songs": [],
                "alias": [
                    "Beyond 25周年精选"
                ],
                "status": 1,
                "copyrightId": 5003,
                "commentThreadId": "R_AL_3_34110",
                "artists": [
                    {
                        "name": "Beyond",
                        "id": 11127,
                        "picId": 0,
                        "img1v1Id": 0,
                        "briefDesc": "",
                        "picUrl": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "albumSize": 0,
                        "alias": [],
                        "trans": "",
                        "musicSize": 0,
                        "topicPerson": 0
                    }
                ],
                "subType": "录音室版",
                "transName": null,
                "onSale": false,
                "mark": 0,
                "gapless": 0,
                "dolbyMark": 0,
                "picId_str": "109951165796417308"
            },
            "starred": false,
            "popularity": 100.0,
            "score": 100,
            "starredNum": 0,
            "duration": 322560,
            "playedNum": 0,
            "dayPlays": 0,
            "hearTime": 0,
            "sqMusic": {
                "name": null,
                "id": 7238157787,
                "size": 35094811,
                "extension": "flac",
                "sr": 44100,
                "dfsId": 0,
                "bitrate": 870405,
                "playTime": 322560,
                "volumeDelta": -49117.0
            },
            "hrMusic": null,
            "ringtone": "600902000004240302",
            "crbt": null,
            "audition": null,
            "copyFrom": "",
            "commentThreadId": "R_SO_4_346089",
            "rtUrl": null,
            "ftype": 0,
            "rtUrls": [],
            "copyright": 1,
            "transName": "Boundless Oceans, Vast Skies",
            "sign": null,
            "mark": 0,
            "originCoverType": 1,
            "originSongSimpleData": null,
            "single": 0,
            "noCopyrightRcmd": null,
            "rtype": 0,
            "rurl": null,
            "mvid": 376199,
            "bMusic": {
                "name": null,
                "id": 7238157782,
                "size": 5162257,
                "extension": "mp3",
                "sr": 44100,
                "dfsId": 0,
                "bitrate": 128000,
                "playTime": 322560,
                "volumeDelta": -44981.0
            },
            "mp3Url": null,
            "hMusic": {
                "name": null,
                "id": 7238157786,
                "size": 12905578,
                "extension": "mp3",
                "sr": 44100,
                "dfsId": 0,
                "bitrate": 320000,
                "playTime": 322560,
                "volumeDelta": -49200.0
            },
            "mMusic": {
                "name": null,
                "id": 7238157785,
                "size": 7743364,
                "extension": "mp3",
                "sr": 44100,
                "dfsId": 0,
                "bitrate": 192000,
                "playTime": 322560,
                "volumeDelta": -46622.0
            },
            "lMusic": {
                "name": null,
                "id": 7238157782,
                "size": 5162257,
                "extension": "mp3",
                "sr": 44100,
                "dfsId": 0,
                "bitrate": 128000,
                "playTime": 322560,
                "volumeDelta": -44981.0
            },
            "transNames": [
                "Boundless Oceans, Vast Skies"
            ]
        }
    ],
    "equalizers": {
        "346089": "rock"
    },
    "code": 200
}


http://music.163.com/api/search/get/web?s=雪の華 Uru&type=1&offset=0&limit=8

{
    "result": {
        "songs": [
            {
                "id": 1331310670,
                "name": "雪の華",
                "artists": [
                    {
                        "id": 12062125,
                        "name": "Uru",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    }
                ],
                "album": {
                    "id": 74151665,
                    "name": "プロローグ",
                    "artist": {
                        "id": 0,
                        "name": "",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    },
                    "publishTime": 1543939200000,
                    "size": 6,
                    "copyrightId": 2706476,
                    "status": 0,
                    "picId": 109951165050805421,
                    "mark": 0
                },
                "duration": 317074,
                "copyrightId": 2706476,
                "status": 0,
                "alias": [],
                "rtype": 0,
                "ftype": 0,
                "transNames": [
                    "雪花"
                ],
                "mvid": 0,
                "fee": 1,
                "rUrl": null,
                "mark": 270336
            },
            {
                "id": 459925524,
                "name": "フリージア",
                "artists": [
                    {
                        "id": 12062125,
                        "name": "Uru",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    }
                ],
                "album": {
                    "id": 35174608,
                    "name": "フリージア ＜通常盤＞",
                    "artist": {
                        "id": 0,
                        "name": "",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    },
                    "publishTime": 1543939200000,
                    "size": 6,
                    "copyrightId": 2706476,
                    "status": 0,
                    "picId": 109951166198352809,
                    "mark": 0
                },
                "duration": 335464,
                "copyrightId": 2706476,
                "status": 0,
                "alias": [
                    "TV动画《机动战士高达 铁血的孤儿 第二季》ED2:TVアニメ「機動戦士ガンダム 鉄血のオルフェンズ」第2期ED2テーマ"
                ],
                "rtype": 0,
                "ftype": 0,
                "transNames": [
                    "小苍兰"
                ],
                "mvid": 5570087,
                "fee": 8,
                "rUrl": null,
                "alias": [
                    "TV动画《机动战士高达 铁血的孤儿 第二季》ED2:TVアニメ「機動戦士ガンダム 鉄血のオルフェンズ」第2期ED2テーマ"
                ],
                "mark": 270336
            },
            {
                "id": 1311347592,
                "name": "remember",
                "artists": [
                    {
                        "id": 12062125,
                        "name": "Uru",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    }
                ],
                "album": {
                    "id": 73470777,
                    "name": "Remember",
                    "artist": {
                        "id": 0,
                        "name": "",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    },
                    "publishTime": 1537891200000,
                    "size": 6,
                    "copyrightId": 2706476,
                    "status": 0,
                    "picId": 109951165050155419,
                    "mark": 0
                },
                "duration": 347167,
                "copyrightId": 2706476,
                "status": 0,
                "alias": [
                    "剧场版动画《夏目友人帐: 缘结空蝉》主题曲"
                ],
                "rtype": 0,
                "ftype": 0,
                "mvid": 10850756,
                "fee": 1,
                "rUrl": null,
                "alias": [
                    "剧场版动画《夏目友人帐: 缘结空蝉》主题曲"
                ],
                "mark": 270336
            },
            {
                "id": 1321382214,
                "name": "プロローグ",
                "artists": [
                    {
                        "id": 12062125,
                        "name": "Uru",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p1.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    }
                ],
                "album": {
                    "id": 74151665,
                    "name": "プロローグ",
                    "artist": {
                        "id": 0,
                        "name": "",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p1.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    },
                    "publishTime": 1543939200000,
                    "size": 6,
                    "copyrightId": 2706476,
                    "status": 0,
                    "picId": 109951165050805421,
                    "mark": 0
                },
                "duration": 302393,
                "copyrightId": 2706476,
                "status": 0,
                "alias": [
                    "日剧《中学圣日记》主题曲"
                ],
                "rtype": 0,
                "ftype": 0,
                "transNames": [
                    "序曲 Prologue"
                ],
                "mvid": 10950771,
                "fee": 1,
                "rUrl": null,
                "alias": [
                    "日剧《中学圣日记》主题曲"
                ],
                "mark": 270336
            },
            {
                "id": 532940331,
                "name": "フリージア",
                "artists": [
                    {
                        "id": 12062125,
                        "name": "Uru",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p1.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    }
                ],
                "album": {
                    "id": 37256748,
                    "name": "モノクローム",
                    "artist": {
                        "id": 0,
                        "name": "",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p1.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    },
                    "publishTime": 1513699200007,
                    "size": 14,
                    "copyrightId": 0,
                    "status": 0,
                    "picId": 109951163118958240,
                    "mark": 0
                },
                "duration": 336480,
                "copyrightId": 754011,
                "status": 0,
                "alias": [
                    "TV动画《机动战士高达：铁血的奥尔芬斯》ED4"
                ],
                "rtype": 0,
                "ftype": 0,
                "transNames": [
                    "小苍兰 Freesia"
                ],
                "mvid": 10951209,
                "fee": 8,
                "rUrl": null,
                "alias": [
                    "TV动画《机动战士高达：铁血的奥尔芬斯》ED4"
                ],
                "mark": 262144
            },
            {
                "id": 1820950546,
                "name": "ドライフラワー",
                "artists": [
                    {
                        "id": 12062125,
                        "name": "Uru",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p1.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    }
                ],
                "album": {
                    "id": 123177141,
                    "name": "ファーストラヴ",
                    "artist": {
                        "id": 0,
                        "name": "",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p1.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    },
                    "publishTime": 1612713600000,
                    "size": 6,
                    "copyrightId": 2706476,
                    "status": -1,
                    "picId": 109951166198037889,
                    "mark": 0
                },
                "duration": 286093,
                "copyrightId": 2706476,
                "status": 0,
                "alias": [],
                "rtype": 0,
                "ftype": 0,
                "mvid": 0,
                "fee": 1,
                "rUrl": null,
                "mark": 270336
            },
            {
                "id": 1311347593,
                "name": "One more time, One more chance",
                "artists": [
                    {
                        "id": 12062125,
                        "name": "Uru",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p1.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    }
                ],
                "album": {
                    "id": 73470777,
                    "name": "Remember",
                    "artist": {
                        "id": 0,
                        "name": "",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p1.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    },
                    "publishTime": 1537891200000,
                    "size": 6,
                    "copyrightId": 2706476,
                    "status": 0,
                    "picId": 109951165050155419,
                    "mark": 0
                },
                "duration": 344764,
                "copyrightId": 2706476,
                "status": 0,
                "alias": [],
                "rtype": 0,
                "ftype": 0,
                "mvid": 0,
                "fee": 1,
                "rUrl": null,
                "mark": 270336
            },
            {
                "id": 1486331346,
                "name": "糸",
                "artists": [
                    {
                        "id": 12062125,
                        "name": "Uru",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    }
                ],
                "album": {
                    "id": 0,
                    "name": "",
                    "artist": {
                        "id": 0,
                        "name": "",
                        "picUrl": null,
                        "alias": [],
                        "albumSize": 0,
                        "picId": 0,
                        "img1v1Url": "http://p2.music.126.net/6y-UleORITEDbvrOLV0Q8A==/5639395138885805.jpg",
                        "img1v1": 0,
                        "trans": null
                    },
                    "publishTime": 0,
                    "size": 0,
                    "copyrightId": 0,
                    "status": 0,
                    "picId": 109951166361057869,
                    "mark": 0
                },
                "duration": 189000,
                "copyrightId": 2707442,
                "status": 0,
                "alias": [],
                "rtype": 0,
                "ftype": 0,
                "transNames": [
                    "线"
                ],
                "mvid": 0,
                "fee": 0,
                "rUrl": null,
                "mark": 262144
            }
        ],
        "songCount": 20
    },
    "code": 200
}

 */