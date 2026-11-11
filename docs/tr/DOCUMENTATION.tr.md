# DevSuite — teknik dokümantasyon

Bu dosya projenin **neden** var olduğunu, **nasıl** yapıldığını, **neyin kullanıldığını**
ve her parçanın **ne işe yaradığını** anlatır. Kodun içindeki yorumlar "ne yapıyor"u
söyler; bu dosya "neden böyle yapıldı"yı söyler.

---

## 1. Problem — bu proje neden var

Başlangıç şikâyeti üç ayrı belirtiydi:

1. Bilgisayar açılışında **birkaç cmd penceresi** yanıp sönüyordu.
2. Makine giderek **yavaşlıyordu**.
3. Disk sürekli doluyordu — **50 GB boş alan 30 GB'a** düşüyordu, sebebi bilinmiyordu.

### Teşhis

**Disk.** WizTree ile C: sürücüsünün MFT'si (NTFS ana dosya tablosu) okundu ve CSV'ye
aktarıldı. Sonuç: `%LOCALAPPDATA%` tek başına **133 GB**. Dağılım:

| GB | Klasör | Ne |
|---|---|---|
| 22.5 | `Google\Chrome\User Data` | profiller + cache + yerel AI modeli |
| 19.2 | `Unity` | 18.6 GB'ı Package Manager cache'i |
| 17.6 | `uv\cache\archive-v0` | Python paket arşivi |
| 10.5 | `Google\DriveFS` | Drive çevrimdışı kopyaları |
| 7.0 | `Android\Sdk` | NDK, emulator, sources, build-tools |
| 2.2 | `npm-cache` | 2 GB'ı `_npx` |
| 2.0 | `SquirrelTemp` | yarım kalmış kurulumlar |

Yani kaybolan alan bir virüs ya da arka planda indirme değil, **geliştirme araçlarının
cache'leri**. Hiçbiri kendini temizlemiyor.

**Açılıştaki cmd pencereleri ve yavaşlama.** Claude Desktop / Claude Code / Codex gibi
istemciler, tanımlı **her MCP sunucusu için ayrı bir process** başlatıyor. Bunlar
`npx` / `uvx` ile çalıştığı için Windows'ta kısa süreliğine konsol penceresi açıyor.
On tane bağlı MCP = on process + on pencere. Buna ek olarak kurulu skill'lerin
açıklamaları her konuşmada bağlama yükleniyor; şişkin açıklamalar modeli yavaşlatıyor.

### Bu üç belirti üç araca dönüştü

- Cache'leri bul, ölç, temizle → **CacheSweep**
- MCP sunucularını gör ve kapat → **MCP Control**
- Yeni MCP'yi doğru şekilde ekle → **MCP Setup**
- Skill'leri gör, kapat, şişkinliği yakala → **Skill Manager**
- Araçları tek yerden aç → **DevSuite Hub**

Ürün disk belirtisinden çıktı: küçük bir Hub'dan açılan CacheSweep. MCP ve skill araçları
sürüme girmeyen deneysel eklerdir; bkz. [`extras/README.md`](../../extras/README.md).

---

## 2. Ne kullanıldı — ve neden

| Seçim | Neden bu |
|---|---|
| **C# / WinForms** | Tek `.exe`, kurulum yok, bağımlılık yok. Electron 150 MB, Tauri Rust ister. |
| **.NET Framework 4.x** | Windows 10 ve 11 ile birlikte **zaten geliyor**. Kullanıcı hiçbir şey indirmiyor. |
| **`csc.exe`** | .NET Framework'le birlikte gelen C# derleyicisi: `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Visual Studio, SDK, MSBuild gerekmiyor. |
| **PowerShell** (derleme) | Derleyiciyi çalıştıran şeyin kendisinin derlenmesi gerekmesin diye. |
| **`robocopy /L`** (ilk sürümde) | Klasör boyutu ölçmede `Get-ChildItem -Recurse`'den kat kat hızlı. Sonradan C#'ın kendi sayıcısına geçildi. |
| **`System.Web.Extensions`** | .NET Framework'ün içindeki `JavaScriptSerializer` — JSON okumak için harici kütüphane gerekmiyor. |
| **`Microsoft.VisualBasic`** | `FileSystem.DeleteDirectory(..., RecycleOption.SendToRecycleBin)` — Geri Dönüşüm Kutusu'na göndermenin tek yerleşik yolu. |
| **Inno Setup** | Ücretsiz, standart Windows kurulum üreticisi. |
| **GitHub Actions** | Sürüm etiketi atınca zip, kurulum dosyası ve özetlerle bir **taslak** sürüm oluşturuyor; yayınlamak insana kalıyor. |

**Kullanılmayanlar ve sebebi:** NuGet paketi yok (tek dosya derlenebilsin diye),
JSON kütüphanesi yok (Framework'ün kendisi yeterli), TOML kütüphanesi yok (satır bazlı
düzenleme daha güvenli — kullanıcının yorumlarını ve sıralamasını bozmuyor).

---

## 3. Mimari — nasıl kurgulandı

```
devsuite/
├─ hub/Hub.cs + app.json            ilk açılan ekran
├─ apps/CacheSweep/                 modül: pencere, MiniWindow, Targets, Sweeper, ScanMemory, RunningTools,
│                                   ConfirmWindow, Widgets
├─ shared/                          ortak kod, uygulamalar manifestte seçer
│  ├─ Tokens.cs  tasarım belirteçleri: boşluk, tipografi, renk, DPI
│  ├─ Native.cs  koyu başlık çubuğu ve kaydırma çubukları (Win32)
│  ├─ Ui.cs      bileşenler ve pencere kabuğu
│  └─ Logo.cs    kodla çizilen logo
├─ extras/                          deneysel araçlar, sürüme dahil değil
├─ tools/build.ps1                  manifest tarayan derleme
├─ tests/                           pencere açmayan testler
└─ installer/ · .github/workflows/  dağıtım
```

### Manifest tabanlı derleme

Derleme betiğinde **hiçbir uygulama adı geçmiyor**. `apps/*/app.json` dosyalarını tarar,
her birinin `sources` ve `references` alanlarını okur, `csc.exe`'ye verir.

```json
{
  "name": "Your App",
  "en": "What it does",
  "color": "#3B82F6", "glyph": "Y",
  "sources":    [ "apps/YourApp/YourApp.cs", "shared/Tokens.cs",
                  "shared/Native.cs", "shared/Ui.cs" ],
  "references": [ ]
}
```

Yeni uygulama eklemek = yeni klasör + `app.json`. Derleme, kurulum dosyası ve yayın ek
değişiklik istemez. Hub'da düğmesi olması için modül listesine bir satır da gerekir; nedeni
aşağıda.

### Hub: listeler ve açar, başka bir şey yapmaz

İlk açılan ekran Hub'dır ve modüller açılmadan iş yapmaz: Hub açılışta yalnızca bilinen her
dosyanın var olup olmadığına bakar. Modül başlatmak bir düğmeye basmayı gerektirir ve dosyadaki
tek `Process.Start` budur; `tests/HubTests.ps1` bunu ve ağ kodu olmadığını denetler.

Önceki sürüm bir mağazaydı: `catalog.json` ile yanındaki her `.exe`'yi birleştirir, yenilerini
indirebilirdi. Bu, klasörde ne varsa çalıştırmanın bir yolu demekti (kaldırıcı bile uygulama
gibi görünüyordu) ve indirme de imzasız bir katalog kadar güvenilirdi. Bütün vaadi "istemediğin
hiçbir şeyi yapmaz" olan bir program için buna değmezdi. Modül listesi artık kodda sabittir;
bir modül ancak tam o dosya Hub'ın yanındaysa görünür.

---

## 4. Uygulamalar — ne işe yarıyor, nasıl çalışıyor

### 4.1 CacheSweep

**Ne yapar.** Tara'ya bastığında bilinen ~30 cache konumunu ölçer, listeler, seçtiklerinin
**içini** boşaltır. Pencereyi açmak iş yaptırmaz; açılışta tarama bir ayardır ve varsayılan
olarak kapalıdır.

**Hedef kuralları.** Liste `apps/CacheSweep/Targets.cs` içindedir ve hedef kural taşıyabilir:
`Needs` çalışmaması gereken programları, `Keep` yerinde kalacak öğeleri (npm altında `_npx`,
Playwright yanında `mcp-*` tarayıcı profilleri), `OlderThan` korunacak yeni dosyaları (Temp'te
yedi gün), `AdvancedOnly` geri getirmesi pahalı öğeleri gizlemeyi belirtir. Windows Update
indirme klasörü eskiden hedefti, kaldırıldı: bir sistem servisine aittir ve temizlemek süren
bir güncellemeyi bozabilir.

**Ölçme ve silme için tek motor.** Bir hedefin hangi öğeleri içerdiğine `Sweeper.cs` karar
verir; boyutu da silmeyi de aynı fonksiyon besler, yani ekrandaki sayı silinecek olandır.
Silme ağacı kendisi gezer, çünkü `Directory.Delete` salt okunur dosyada hata verir ve dosyalar
arasında iptal sunmaz. Bağlantıların içine girilmez, yalnızca kendileri kaldırılır.

**Hatırlanan tarama.** `ScanMemory.cs` son taramanın ölçtüklerini tutar; pencere bunları hemen
gösterir ve açılışta ölçüm yapmaz. Hızlı tarama her hedefin parmak izini (üç seviye aşağıya
kadar ad, tarih ve boyut, en fazla 20.000 öğe) karşılaştırır, yalnızca farklı olanları yeniden
ölçer. Geliştirildiği bilgisayarda parmak izleri toplam 352 ms, aynı önbellekleri tek tek gezmek
41,5 sn sürdü; ölçüm disk önbelleği ısınmışken yapıldı.

Bu hızın bedeli var: klasör tarihi yalnızca doğrudan içindekileri yansıtır, daha derindeki bir
değişiklik kaçabilir. Bu yüzden üç kural var. Yeniden kullanılan boyut `~` ile işaretlenir. Yaş
kuralı olan hedefler her zaman ölçülür, çünkü cevapları saate göre değişir. Seçilen klasörler
onaydan önce yeniden ölçülür, yani hatırlanan bir sayı onaya ulaşmaz. Dosya yalnızca boyut verir:
her hedefin kendi anahtarıyla aranır, listeye asla klasör ekleyemez. Yanlış alarmın bedeli tek
bir fazladan ölçümdür.

**Küçük modlar.** `MiniWindow.cs` küçük bir panel ve yuvarlak bir simge içerir; ikisi de
sürüklenebilir ve üstte kalır. Tam pencere arkada gizli olarak yaşamaya devam eder. Zamanlayıcıyla
okudukları tek şey boş disk alanıdır: yalnızca biri görünürken, beş saniyede bir tek sistem çağrısı.
Klasör boyutları son taramadan gelir, ölçmenin tek yolu Hızlı tara'dır ve silme yetkileri yoktur:
silme her zaman tam penceredeki onaydan geçer.

**Onay.** `ConfirmWindow.cs` her yolu kaydırılabilir bir kutuda listeler, neyin neden
bekletildiğini söyler ve "Hiçbirini silme" seçili açılır; Enter'a basmak hiçbir şey silmez.
Ana pencere silme sırasında kapanmayı reddeder, iptal dosyalar arasında durur ve kalanı bildirir.
Bir mutex tek kopya çalışmasını sağlar, böylece iki pencere aynı klasörden silemez.

**Nasıl ölçer.** Klasör boyutu ölçmek = içindeki her dosyayı gezmek. `uv` cache'inde
yüz binlerce küçük dosya var; bu yüzden Explorer ve çoğu script orada donmuş gibi
görünür. CacheSweep bunu üç şeyle çözer:

1. **Paralel ölçüm** — `Parallel.ForEach`, çekirdek sayısı kadar iş parçacığı. Toplam
   süre en yavaş tek klasör kadar, hepsinin toplamı kadar değil.
2. **Yığın tabanlı yürüyüş** — özyineleme yok (`Stack<string>`), bir klasörde erişim
   hatası tüm taramayı düşürmüyor.
3. **Junction/symlink atlama** — `FileAttributes.ReparsePoint` kontrolü, sonsuz döngüyü
   engelliyor.

**Güvenlik kararları.**
- Hiçbir kutu önceden işaretli değil.
- Onay penceresi **etkilenecek her yolu tek tek** listeler.
- Sadece klasörün **içi** silinir, klasörün kendisi kalır. Yalnızca hedef klasörün
  doğrudan altındaki öğelere dokunulur; junction ve symlink'ler takip edilmez, bağlantı
  kaldırılır.
- **Tek kapı.** Her silme `DeleteEntry` üzerinden, her özel klasör `Guard.Reject`
  üzerinden geçer. Denetim üç kez çalışır: klasör eklenirken, ayarlar yüklenirken ve
  içi silinmeden hemen önce. Böylece eklendiğinde zararsız olup sonradan projeye dönüşen
  bir klasör silme anında reddedilir. Reddedilenler: sürücü kökleri, ağ yolları,
  kullanıcı profili ve bilinen klasörleri (Masaüstü, Belgeler, İndirilenler, Resimler,
  AppData, OneDrive, kimlik bilgisi ve AI istemci durumu tutan nokta-klasörler), Windows
  ve Program Files (her iki yönde), korumalı bir kökle çakışan her şey, proje işareti
  taşıyan klasörler (`.git`, `.sln`, `package.json`, `pyproject.toml`, `Cargo.toml`,
  `go.mod`, `Assets` + `ProjectSettings` ve benzerleri) ve alt klasörleri birer proje
  olan klasörler — bir `repos` klasörü cache değildir.
- **Unity Asset Store indirmeleri ve Chrome profil verisi yol düzeyinde korumalı.**
  Asset Store indirmeleri listede görünür, boyutu yazar, kutusu işaretlenemez; Chrome'un
  tüm kanallarındaki `User Data` klasörü özel klasör olarak bile eklenemez. Ayrıca
  silinecek her öğe silinmeden hemen önce korumalı köklerle tek tek karşılaştırılır.
- **Her yol iki kez denetlenir:** yazıldığı haliyle ve gerçekte çıktığı yerde. Windows,
  junction, symlink ve `subst` sürücülerinin ardındaki son konumu bildirir
  (`GetFinalPathNameByHandle`); yani Belgeler'e giden bir junction, Belgeler sayılır.
  Çözümlenemeyen yol reddedilir. Yönlendirilmiş yerleşik hedefler de tam denetimden
  geçer ve onay penceresi gerçek konumu gösterir. Cache içindeki, korumalı bir köke giden
  junction'a dokunulmaz, engellendi olarak raporlanır. `tests/GuardTests.ps1` junction
  senaryolarını sınar.
- **Paket yöneticisi komutu yok.** Önceki sürüm silme sonrası `uv cache clean`,
  `npm cache clean --force`, `pnpm store prune` ve `pip cache purge` çalıştırıyordu. Bu
  komutlar onay penceresinin dışında siliyor, Geri Dönüşüm Kutusu ayarını görmezden
  geliyor ve hedefi yerelleştirilmiş görünen adından tanıyordu. Kaldırıldı; klasörler
  diğer hedefler gibi boşaltılır, paket yöneticileri bir sonraki çalışmada indeksini
  kendisi yeniden kurar.
- **Geri Dönüşüm Kutusu.** Ayar, arka plan işi başlamadan önce bir kez okunur; iş
  sürerken Ayarlar ve Dışa Aktar düğmeleri kapalıdır. `SendToRecycleBin` +
  `OnlyErrorDialogs` kutuya sığmayan öğeleri sormadan kalıcı siler; bu yüzden seçenek
  metni ve onay penceresi bunu açıkça söyler. Bir silme ancak yol gerçekten kaybolduysa
  başarılı sayılır.
- Kilitli dosyalar atlanır ve sonunda raporlanır; yol analiz edilip muhtemel suçlu
  tahmin edilir ("Muhtemel sebep: uv / Python").
- İptal edilen ölçüm `-1` döner ve `?` gösterir, yarım bir sayı kesinmiş gibi
  görünmez; hedefsiz tarama hemen biter, pencere meşgul kalmaz; eski bir taramanın geç
  gelen sonuçları kuşak sayacıyla yok sayılır; `settings.ini` içindeki bozuk bir
  `custom=` satırı açılışta çökme yerine atlanır.

**Ayarlar** `%APPDATA%\CacheSweep\settings.ini` içinde saklanır: tema, "şu MB altını
gizle" eşiği, açılışta tarama (varsayılan kapalı), geri dönüşüm modu, kullanıcının eklediği
klasörler.

Aşağıdaki araçlar deneyseldir, `extras/` altında durur ve sürüme dahil değildir. Bilinen
sorunları [`extras/README.md`](../../extras/README.md) dosyasında listelenir.

### 4.2 MCP Control

**Ne yapar.** Üç istemcideki MCP sunucularını tek listede gösterir, silmeden açar kapatır.

| İstemci | Ayar dosyası | Biçim |
|---|---|---|
| Claude Desktop | `%APPDATA%\Claude\claude_desktop_config.json` | JSON |
| Claude Code | `%USERPROFILE%\.claude.json` | JSON |
| Codex / ChatGPT | `%USERPROFILE%\.codex\config.toml` | TOML |

**Kapatma nasıl çalışıyor — iki farklı yöntem, sebebiyle birlikte.**

*Codex/ChatGPT:* TOML biçiminde **resmî bir `enabled` alanı var**. Dosya satır satır
işlenir, ilgili `[mcp_servers.<ad>]` bloğu bulunur, `enabled = false` satırı yazılır
veya eklenir. Dosyanın geri kalanına — yorumlarına, sıralamasına, biçimine —
dokunulmaz. TOML kütüphanesi bilerek kullanılmadı: yeniden serileştirme kullanıcının
dosyasını yeniden yazardı.

*Claude tarafı:* Böyle bir alan yok. Girdi `mcpServers`'tan alınıp `mcpServersDisabled`
adlı ayrı bir bölüme **taşınır**. Claude bu anahtarı tanımadığı için görmezden gelir;
sunucu başlamaz ama ayarları olduğu gibi durur. Geri açınca aynen yerine döner.

**Yazma kuralları** (`extras/shared/Mcp.cs`): her değişiklikten önce tarih damgalı, aynı
saniye içinde bile benzersiz bir `.bak`; yeni metin geçici dosyaya yazılıp `File.Replace`
ile yerine konur, yazma sırasında çökme yarım dosya bırakamaz; orijinalin satır sonu
biçimi ve BOM'u korunur; sayılar `R` biçimiyle kayıpsız döner. Okunamayan dosya
`Mcp.Errors` listesine girer, bir kez gösterilir ve asla yazılmaz. Hem `mcpServers` hem
`mcpServersDisabled` altında bulunan bir sunucu sessizce ezilmez, reddedilir. TOML
tarafında `enabled = true  # yorum` satırı yorumunu korur, çok satırlı diziler tablo
sonu sanılmaz, `[mcp_servers.x.env]` alt tablosu sunucunun kendi anahtarlarıyla
karıştırılmaz, var olan bir tabloya ekleme yapılırsa kopya eklemek yerine tablo yerinde
değiştirilir. Sunucu adları (`[A-Za-z0-9_-]`, en fazla 64) dosyaya ulaşmadan doğrulanır;
nokta veya boşluk TOML tablo adını bozar.

Altta özet: **"14 sunucu · 11 açık · 3 kapalı — açılışta 11 işlem başlıyor"**.
Bu satır, konuşmanın başındaki "açılışta neden cmd pencereleri açılıyor" sorusunun
doğrudan cevabı.

### 4.3 MCP Setup

**Ne yapar.** Yeni MCP sunucusu ekler.

- **Hazır şablonlar** — filesystem, github, memory, postgres, puppeteer, uvx.
- **Komutu dene** — komutu gerçekten çalıştırır, 6 saniye bekler, sonra durdurur.
  MCP sunucuları normalde açık kalır, o yüzden "çalıştı" demek çıkış kodundan değil
  patlamamış olmasından anlaşılır.
- **Canlı önizleme** — hem JSON hem TOML karşılığını yazarken gösterir.
- **Çoklu hedef** — seçtiğin istemcilerin hepsine tek seferde yazar, her biri kendi
  biçiminde.
- Aynı isim varsa üzerine yazmadan önce sorar.

### 4.4 Skill Manager

**Ne yapar.** Kurulu skill'leri listeler, açar kapatır, `SKILL.md` düzenler ve bağlamı
şişirenleri işaretler.

**Taranan yerler:** `~/.claude/skills`, `~/.claude/plugins`, `%APPDATA%\Claude\skills`.
Her klasörde `SKILL.md` aranır; marketplace ve cache düzenleri
(`plugins/cache/<market>/<eklenti>/<sürüm>/skills/<ad>`) için derinlik 7'ye kadar
inilir, `node_modules`, `.git` ve reparse point'ler atlanır. Kökler büyük/küçük harf
duyarsız tekilleştirilir (NTFS için `skills` ile `Skills` aynı klasördür).

**Sağlayıcı tespiti.** Skill'in kimden geldiği **yoldan çıkarılır**:
`…/plugins/<eklenti>/skills/<ad>` → eklenti adı (`skills`'ten önceki klasör; o bir sürüm
numarasına benziyorsa bir öncesi). `~/.claude/skills/<ad>` → "kişisel".
Her tür farklı renkte rozetle gösterilir.

**Aç/kapat.** Kapatma = klasörü aynı yerdeki `_disabled/` altına **taşımak**. Hiçbir şey
silinmez, açınca geri taşınır. Üst klasörlerden herhangi biri `_disabled` ise skill
kapalı sayılır. Yapılamayan bir taşıma (hedef zaten var, zaten kapalı) sessizce
geçiştirilmez, nedeniyle birlikte hata verir.

**Uyarılar — neden bunlar.** Skill açıklamaları her konuşmada bağlama yüklenir. Şişkin
veya çakışan skill'ler hem maliyet hem yavaşlık üretir. İşaretlenenler:
- aynı isimde birden fazla skill (hangisinin çalışacağı belirsiz)
- açıklaması olmayan skill (model ne zaman kullanacağını bilemez)
- 500 karakteri aşan açıklama
- 50 KB'ı aşan `SKILL.md`
- "always use", "every message", "any task" gibi aşırı geniş tetikleyiciler

Üstte arama kutusu, durum filtreleri (Tümü / Açık / Kapalı / Uyarılı — sayılarıyla) ve
sıralama (uyarı / ad / sağlayıcı / boyut / tarih). Düzenlemeden önce `.bak` alınır.

### 4.5 DevSuite Hub

Yukarıda anlatıldı: listeler ve açar. MCP Control, MCP Setup ve Skill Manager deneyseldir,
`extras/` altında durur ve sürüme dahil değildir.

---

## 5. Tasarım sistemi

İlk sürümde her bileşen kendi renklerini ve ölçülerini taşıyordu; sonuç tutarsız ve
prototip görünümlüydü. Yeniden kuruldu — `shared/Tokens.cs` içinde, hiçbir bileşen
kendi sayısını taşımıyor. Oradaki her ölçü mantıksal piksel, yani ekran 96 dpi'ymiş
gibi yazılmış: `Dpi.Px` bunu Windows'un yerleştirdiği şeyler için gerçek piksele
çevirir, özel çizim ise `Draw.Quality`'nin önceden ölçeklediği bir tuvale çizer;
böylece çizim kodu yazıldığı sayılarla çalışmaya devam eder. Kod içindeki gerçek isimler (aşağıda) İngilizce; burada sadece
ne işe yaradıkları anlatılıyor:

- `Space` — 8pt ızgara: 4 / 8 / 12 / 16 / 20 / 24 / 32
- `Fonts` — H1, H2, Title, Body, Label, Small, Micro, Numeric, Mono
- `Palette` — **anlamsal** isimler (`Accent`, `Warning`, `TextSecondary`), "mavi" değil
  — tema değişince isim yanlış olmasın diye.

`Palette.Apply(dark)` tüm token'ları yeniden atar ve `Changed` olayını tetikler;
pencereler kendini yeniden boyar. Tercih `%APPDATA%\DevSuite\theme.txt`'ye yazılır, tüm
uygulamalar aynı dosyayı okur.

`Loc` iki ayrı sözlük tutar: her uygulamanın `Add()` ile kaydettiği yerleşik İngilizce
metinler, ve `lang.json`'dan gelen metinler. İkisi ayrı tutulur ki bir uygulamanın
`Add()`'i hangisi önce çalışırsa çalışsın bir çeviriyi asla ezmesin.

Bileşenler: `UiButton` (gerçek hover/basılı/devre-dışı durumlu beş varyant), `Row`
(durum noktası, başlık, rozetler, ikincil metin, orantılı çubuklu sağ değer, hover'da
çıkan eylemler), `FilterBar` (sayı gösteren çipler), `SearchBox`, `EmptyState`,
`ProgressLine`, `Shell` (üst başlık, araç çubuğu, liste, dip şerit).

---

## 6. Yaşanan hatalar ve çözümleri

Bunlar burada duruyor çünkü tekrar düşülmesi kolay.

**`Timer` belirsizliği.**
`using System.Threading;` ve `using System.Windows.Forms;` bir arada olunca `Timer`
adı iki yerde birden bulunuyor, derleyici `CS0104` veriyor.
→ Tam adıyla `System.Windows.Forms.Timer` yazıldı.

**WinForms dock sırası.**
Dock yerleşimi `Controls` listesini **sondan başa**, yani son eklenenden ilk eklenene
doğru işler; `Controls[0]` **en son** yerleşir ve daha sonra eklenen her kardeşin
bıraktığı boş alanı alır. Yani `Fill` olan panel **en önce eklenmeli** (`Controls[0]` o
olacak şekilde), kenara yaslanan şeritler ondan sonra, en dıştaki şerit ise **en son**
eklenmeli. Yanlış sırada eklendiğinde Fill tüm pencereyi yutuyor, şeritler onun üzerine
biniyordu. `BringToFront()` çağrıları durumu büsbütün karıştırıyordu.
→ Sıra sabitlendi: `Liste (Fill) → Dip (Bottom) → AraÇubuk (Top) → Üst (Top, en son)`,
`BringToFront` yok.

**Ölçüm donması.**
İlk PowerShell sürümü klasörleri sırayla ölçüyordu ve `uv` cache'inde kilitleniyordu.
→ Önce `robocopy /L` + zaman aşımı, sonra C#'ta paralel ölçüme geçildi.

**İsim çakışmaları.**
`DevSuite.Button` ve `DevSuite.Type`, her iki namespace'i de import eden uygulamalarda
`System.Windows.Forms.Button` ve `System.Type` ile çakışıyordu (`CS0104`).
→ `UiButton` ve `Fonts` olarak yeniden adlandırıldı. Aynı mantıkla, hiçbir alan
gölgelediği bir tipin adını taşımıyor (bir kontrol üzerinde `Path`, `Version`, `Text`
gibi).

**csc.exe ve karakter kodlaması.**
Framework'le gelen derleyici, BOM'suz bir kaynak dosyayı sistemin kod sayfasında okur.
→ ASCII dışı karakter (©, —, ⚠) içeren her `.cs` dosyası UTF-8 BOM taşır; `.bat`
dosyaları hiçbir zaman taşımaz.

---

## 7. Dağıtım

**Yerel.** `BUILD.bat` → `tools\build.ps1` → `dist\` ve `dist-tr\`; `-Extras` ile ayrıca
`dist-extras\`. Derleme yalnızca bu klasörlere yazar.

**Yayın.** `v0.9.0` gibi bir etiket Windows makinesinde derler, testleri çalıştırır, iki
portable zip (İngilizce ve İngilizce + Türkçe paket), kullanıcı başına kurulan Inno Setup
dosyası (Türkçe paket isteğe bağlı bileşen) ve `SHA256SUMS.txt` üretir, bir **taslak** sürüm
oluşturur. Biri Yayınla'ya basana kadar hiçbir şey herkese açık olmaz.

**İmzalama — mevcut durum.** Binary'ler imzalı değil, bu yüzden SmartScreen ilk
çalıştırmada uyarıyor (*Daha fazla bilgi → Yine de çalıştır*).

| Seçenek | Maliyet | Durum |
|---|---|---|
| İmzasız | 0 | şu anki durum, SHA256 ile doğrulanabilir |
| Kendinden imzalı | 0 | **işe yaramıyor**, SmartScreen yine engelliyor |
| Azure Artifact Signing | ~$9.99/ay | bireysel geliştiricilerde sadece ABD/Kanada |
| OV sertifika | $150–300/yıl | uygulanabilir |
| EV sertifika | $400+/yıl | 2024'ten beri OV'den avantajı yok |
| SignPath (açık kaynak) | ücretsiz | başvuru gerekiyor, en makul hedef |

İmzalama henüz yayın iş akışına bağlı değil.

---

## 8. Kısıtlar ve bilinen eksikler

- **Sadece Windows.** WinForms ve .NET Framework'e bağlı.
- **CacheSweep ortak koda taşınmadı** — kendi tema/düğme kopyasını taşıyor.
  `Tokens.cs` ve `Native.cs`'i paylaşıyor, yani ölçekleme ve pencere kaplaması
  diğerleriyle aynı davranıyor; bileşenler hâlâ ikinci bir kopya.
- **Skill Manager (deneysel): skill kökleri sabit.** Proje bazlı `.claude/skills` klasörleri taranmıyor.
- **Hub güncelleme denetlemez.** Bu bir tercih: Hub'da ağ kodu yok. Yeni sürümler sürümler
  sayfasında.
- **Testler pencereyi kapsamıyor.** Denetim, silme motoru ve Hub'ın listeledikleri test
  ediliyor; pencereler ve tıklamalar elle kontrol ediliyor.
- **Denetim bir kural listesidir, kanıt değil.** Elle konulmuş değerli bir dosya bir
  cache klasörünün içinde durabilir. Onay penceresini oku.

---

MIT © 2026 Enes Aksu
