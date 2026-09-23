using System.Collections.Generic;

namespace ooor.Core
{
    /// <summary>
    /// GitHub Release 中的单个资产文件。
    /// 直接采用 snake_case 字段名，与 GitHub JSON 一致；
    /// JavaScriptSerializer 默认大小写不敏感、字段名匹配，可直接反序列化。
    /// </summary>
    public class GithubAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
    }

    /// <summary>GitHub Release 一个版本的元数据</summary>
    public class GithubRelease
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public bool prerelease { get; set; }
        public string published_at { get; set; }
        public string tarball_url { get; set; }
        public string zipball_url { get; set; }
        public List<GithubAsset> assets { get; set; }
    }
}