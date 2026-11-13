# Chain Drop (çalışma adı)

Hyper-casual mobil oyun (gerçek 3D): robotları asılı kasanın altına çek, zinciri vur, kasa üstlerine düşsün.

## Kurulum (5 dakika)

1. **Unity Hub** → New project → **Universal 3D** (Unity 6 LTS) → proje adı `ChainDrop`.
2. Bu repodaki `Assets/ChainDrop` klasörünü projenin `Assets/` klasörüne kopyala
   (ya da proje klasöründe: `git init && git remote add origin <repo> && git pull origin main`).
3. Unity'de herhangi bir sahneyi aç (`SampleScene` yeter) → **Play**.
   Sahneye hiçbir şey eklemen gerekmiyor; oyun kendini koddan kuruyor.
4. Game penceresinde çözünürlüğü dikey seç (ör. 1080x1920), oyun telefonda dik oynanır.

## Kontroller

- **Ekranda boş bir yere bas ve sürükle:** karakter o yöne koşar (yüzen joystick). Editörde WASD / ok tuşları da çalışır.
- **Kasaya (ya da zincirine) dokun:** karakter nişan alıp zinciri vurur, kasa düşer.
- Kasanın altındaki **kırmızı işaret** düşeceği yeri gösterir. Kasa ne kadar yüksekse o kadar geç düşer.
- Düşen kasa robotu ezer, ama **seni de ezer**.
- Editörde sol altta `<` `>` düğmeleriyle bölüm atlayabilirsin (sadece test için).

## Bölüm tasarlama

Bütün bölümler tek dosyada: `Assets/ChainDrop/Resources/ChainDrop/levels.json`

Arena zemini: `x = -5 … 5` (sol/sağ), `z = -7 … 6` (yakın/uzak). Yükseklik `h`.

| Alan | Anlamı |
|---|---|
| `theme` | 0 Fabrika, 1 Maden, 2 Liman, 3 Santral, 4 Robot Üssü |
| `shots` | mermi sayısı |
| `par` | 3 yıldız için en fazla kaç mermi |
| `px`, `pz` | oyuncunun başlangıç noktası |
| `crates[].x`, `z` | kasanın zemindeki yeri (kırmızı işaret) |
| `crates[].h` | kasanın alt yüzünün yerden yüksekliği (4 normal, 7 yüksek = geç düşer) |
| `crates[].size` | 2.2 normal, 3.0+ büyük (birden fazla robot ezer) |
| `crates[].swing`, `axis` | sallanma genliği ve yönü (`"x"` / `"z"`), 0 = sabit |
| `robots[].x`, `z` | çıkış kapısı (duvarın hemen içi: arka `z=5.4`, ön `z=-6.4`, sol `x=-4.4`, sağ `x=4.4`) |
| `robots[].delay` | kaçıncı saniyede çıkar |
| `robots[].type` | `walker` normal, `runner` hızlı, `tank` 2 vuruş ister, `scout` kırmızı işarete basmaz ve düşen kasadan kaçar |
| `crates[].rail`, `rx`, `rz` | `true` ise kasa raylı vinçte; duvardaki renkli düğme vurulunca (`rx`,`rz`) noktasına kayar |
| `barrels[].x`, `z` | patlayıcı varil: üstüne kasa düşerse ya da yakınında patlama olursa patlar |

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

Modeller şu an Unity'nin hazır 3D şekillerinden (küp, silindir, küre) kuruluyor. Sonraki adımda gerçek 3D modellerle değiştirilecek.
