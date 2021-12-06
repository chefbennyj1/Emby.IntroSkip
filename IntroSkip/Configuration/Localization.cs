using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace IntroSkip.Configuration
{
    public class Localization
    {
        public static string MatchCountryCodeToDisplayName(string language)
        {
            var cinfo = CultureInfo.GetCultures(CultureTypes.AllCultures & ~CultureTypes.NeutralCultures);
            return cinfo.FirstOrDefault(c => c.DisplayName.Contains(language))?.TwoLetterISOLanguageName;
        }

        //Dictionary is Two Letter ISO name 
        public static Dictionary<string, string> Languages = new Dictionary<string, string>()
        {
            //Arabic (Saudi Arabia)
            {"ar", "تم تخطي المقدمة"},
            //Bulgarian (Bulgaria)
            {"bg", "Въведението е пропуснато"},
            //Catalan (Catalan)
            {"ca", "S'ha omès la introducció"},
            //Chinese (Taiwan)
            {"zh", "简介已跳过"},
            //Czech (Czech Republic)
            {"cs", "Úvod přeskočen"},
            //Danish (Denmark)
            {"da", "Intro springet over"},
            //German (Germany)
            {"de", "Einführung übersprungen"},
            //Greek (Greece)
            {"el", "Η εισαγωγή παραβλέφθηκε"},
            //English 
            {"en", "Intro Skipped"},
            //Finnish (Finland)
            {"fi", "Johdanto ohitettu"},
            //French (France)
            {"fr", "Introduction ignorée"},
            //Hebrew (Israel)
            {"he", "דילוג על הקדמה"},
            //Hungarian (Hungary)
            {"hu", "Intro Kihagyva"},
            //Icelandic (Iceland)
            {"is", "Inngangi sleppt"},
            //Italian (Italy)
            {"it", "Intro saltato"},
            //Japanese (Japan)
            {"ja", "イントロスキップ"},
            //Korean (Korea)
            {"k", "인트로 건너뜀"},
            //Dutch (Netherlands)
            {"nl", "Inleiding overgeslagen"},
            //Norwegian, BokmÃ¥l (Norway)
            {"nb", "Intro hoppet over"},
            //Polish (Poland)
            {"pl", "Pominięto wprowadzenie"},
            //Portuguese (Brazil)
            {"pt", "Introdução pulada"},
            //Romanian (Romania)
            {"ro", "Introducere omisă"},
            //Russian (Russia)
            {"ru", "Вступление пропущено"},
            //Croatian (Croatia)
            {"hr", "Uvod preskočen"},
            //Slovak (Slovakia)
            {"sk", "Uvod preskočen"},
            //Albanian (Albania)
            {"sq", "Hyrja u anashkalua"},
            //Swedish (Sweden)
            {"sv", "Introt hoppade över"},
            //Thai (Thailand)
            {"th", "ข้ามบทนำ"},
            //Turkish (Turkey)
            {"tr", "Giriş Atlandı"},
            //Urdu (Islamic Republic of Pakistan)
            {"ur", "تعارف چھوڑ دیا گیا۔"},
            //Indonesian (Indonesia)
            {"id", "Intro Dilewati"},
            //Ukrainian (Ukraine)
            {"uk", "Вступ пропущено"},
            //Belarusian (Belarus)
            {"be", "Увядзенне прапушчана"},
            //Slovenian (Slovenia)
            {"sl", "Uvod preskočen"},
            //Vietnamese (Vietnam)
            {"vi", "Đã bỏ qua phần giới thiệu"},
            //Afrikaans (South Africa)
            {"af", "Inleiding oorgeslaan"},
            //Hindi (India)
            {"hi", "परिचय छोड़ दिया गया"},
            //Punjabi (India)
            {"pa", "ਜਾਣ-ਪਛਾਣ ਛੱਡੀ ਗਈ"},
            //Spanish (Mexico)
            {"es", "Intro omitido"},
            //Undefined
            {"und", "Intro Skipped"}

        };
    }
}
  