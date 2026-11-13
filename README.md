# Chain Drop (çalışma adı)

Hyper-casual mobil oyun: robotları kasanın altına çek, zinciri vur, kasa üstlerine düşsün.

## Kurulum (5 dakika)

1. **Unity Hub** → New project → **Universal 2D** (Unity 6 LTS) → proje adı `ChainDrop`.
2. Bu repodaki `Assets/ChainDrop` klasörünü projenin `Assets/` klasörüne kopyala
   (ya da proje klasöründe: `git init && git remote add origin <repo> && git pull origin main`).
3. Unity'de herhangi bir sahneyi aç (`SampleScene` yeter) → **Play**.
   Sahneye hiçbir şey eklemen gerekmiyor; oyun kendini koddan kuruyor.
4. Game penceresinde çözünürlüğü dikey seç (ör. 1080x1920), oyun telefonda dik oynanır.

## Kontroller

- **Ekranda boş bir yere bas ve sürükle:** karakter o yöne koşar (yüzen joystick). Editörde WASD / ok tuşları da çalışır.
- **Kasaya (ya da zincirine) dokun:** karakter nişan alıp zinciri vurur, kasa düşer.
- Kasanın **yerdeki gölgesi** düşeceği yeri gösterir. Gölge küçük ve soluksa kasa yüksektedir, geç düşer.
- Düşen kasa robotu ezer, ama **seni de ezer**.
- Editörde sol altta `<` `>` düğmeleriyle bölüm atlayabilirsin (sadece test için).

## Bölüm tasarlama

Bütün bölümler tek dosyada: `Assets/ChainDrop/Resources/ChainDrop/levels.json`

Arena: `x = -5 … 5`, `y = -7 … 6` (kuşbakışı zemin).

| Alan | Anlamı |
|---|---|
| `theme` | 0 Fabrika, 1 Maden, 2 Liman, 3 Santral, 4 Robot Üssü |
| `shots` | mermi sayısı |
| `par` | 3 yıldız için en fazla kaç mermi |
| `px`, `py` | oyuncunun başlangıç noktası |
| `crates[].x`, `y` | kasanın gölgesinin yeri |
| `crates[].h` | yükseklik (3 alçak, 5 normal, 8 çok yüksek = geç düşer) |
| `crates[].size` | 1.8 normal, 2.8+ büyük (birden fazla robot ezer) |
| `crates[].swing`, `axis` | sallanma genliği ve yönü (`"x"` / `"y"`), 0 = sabit |
| `robots[].x`, `y` | çıkış kapısı (duvarın hemen içi: üst `y=5.4`, alt `y=-6.4`, sol `x=-4.4`, sağ `x=4.4`) |
| `robots[].delay` | kaçıncı saniyede çıkar |
| `robots[].type` | `walker` normal, `runner` hızlı, `tank` 2 kasa ister |

## Kod yapısı

| Dosya | Görev |
|---|---|
| `GameManager.cs` | Açılış, bölüm yükleme, giriş, kazan/kaybet, efektler, geçici arayüz |
| `Crate.cs` | Asılı kasa, gölgesi, zinciri, düşüşü |
| `Robot.cs` | Kovalayan robot, engelden kaçınma, ezilme |
| `Player.cs` | Koşan, nişan alan karakter |
| `Theme.cs` | Dünya renk paletleri |
| `Sfx.cs` | Koddan üretilen geçici sesler |
| `LevelData.cs` | JSON bölüm formatı |

Görseller şu an koddan çizilen kutulardan oluşuyor. Sonraki adımda sprite'larla değiştirilecek.
