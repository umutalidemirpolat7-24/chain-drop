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

- **Ekranın alt kısmı (zeminin altı):** sürükleyerek karakteri sağa sola götür. Robotlar hep sana yürür.
- **Ekranın geri kalanı:** dokunduğun yere ateş edersin. Zincire yakın vurursan zincir kopar.
- Düşen kasa robotu ezer, ama **seni de ezer**.
- Editörde sol altta `<` `>` düğmeleriyle bölüm atlayabilirsin (sadece test için).

## Bölüm tasarlama

Bütün bölümler tek dosyada: `Assets/ChainDrop/Resources/ChainDrop/levels.json`

| Alan | Anlamı |
|---|---|
| `theme` | 0 Fabrika, 1 Maden, 2 Liman, 3 Santral, 4 Robot Üssü |
| `shots` | mermi sayısı |
| `par` | 3 yıldız için en fazla kaç mermi |
| `playerX` | başlangıç konumu (-5 … 5) |
| `crates[].x` | kasanın yatay konumu |
| `crates[].chain` | zincir uzunluğu (kısa = yüksek = geç düşer) |
| `crates[].size` | 1.4 normal, 2.0+ büyük (birden fazla robot ezer) |
| `crates[].swing` | 0 sabit, ± sallanan sarkaç |
| `robots[].side` | `L` soldan, `R` sağdan |
| `robots[].delay` | kaçıncı saniyede çıkar |
| `robots[].type` | `walker` normal, `runner` hızlı, `tank` 2 kasa ister |

Dünya koordinatları: zemin `y = 0`, tavan `y = 11`, görünen alan yaklaşık `x = -5.5 … 5.5`.

## Kod yapısı

| Dosya | Görev |
|---|---|
| `GameManager.cs` | Açılış, bölüm yükleme, giriş, kazan/kaybet, efektler, geçici arayüz |
| `HangingCrate.cs` | Zincirli kasa (HingeJoint2D zinciri) + `ChainLink` |
| `Robot.cs` | Düşman yapay zekâsı ve ezilme |
| `Player.cs` | Yem karakter |
| `Theme.cs` | Dünya renk paletleri |
| `Sfx.cs` | Koddan üretilen geçici sesler |
| `LevelData.cs` | JSON bölüm formatı |

Görseller şu an koddan çizilen kutulardan oluşuyor. Sonraki adımda sprite'larla değiştirilecek.
