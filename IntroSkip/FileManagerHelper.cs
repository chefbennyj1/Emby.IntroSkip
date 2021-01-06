using System.IO;
using System.Linq;
using System.Reflection;

namespace IntroSkip
{
    public class FileManagerHelper
    {
       // ReSharper disable once TooManyArguments
        protected void CopyEmbeddedResourceStream(Stream stream, string location, string fileName, char separator)
        {
            var fileStream = new FileStream($"{location}{separator}{fileName}", FileMode.CreateNew);
            for (int i = 0; i < stream.Length; i++) fileStream.WriteByte((byte)stream.ReadByte());
            fileStream.Close();
        }

        protected Stream GetEmbeddedResourceStream(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name     = assembly.GetManifestResourceNames().Single(s => s.EndsWith(resourceName));
            return GetType().Assembly.GetManifestResourceStream(name);
        }
    }
}
