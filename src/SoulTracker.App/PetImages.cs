using System.IO;
using System.Windows.Media.Imaging;
namespace SoulTracker.App;
internal static class PetImages
{
    private static readonly Dictionary<int,BitmapImage?> Cache=[];
    internal static BitmapImage? Get(int id)
    {
        if(Cache.TryGetValue(id,out var image))return image;
        var path=Path.Combine(AppContext.BaseDirectory,"pets",id+".png");
        if(File.Exists(path))
        {image=new();image.BeginInit();image.UriSource=new Uri(path);image.CacheOption=BitmapCacheOption.OnLoad;image.EndInit();image.Freeze();}
        Cache[id]=image;return image;
    }
}
