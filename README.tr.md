# Wordlist & Log Viewer

[English README](README.md)

Wordlist & Log Viewer, yüzlerce gigabayt büyüklüğündeki düz metin loglarını, sözlük ve wordlist dosyalarını tamamını RAM'e yüklemeden açan ve birebir arayan, salt okunur bir Windows x64 uygulamasıdır. 300 GB üzerindeki dosyalar düşünülerek geliştirilmiştir.

**Uygun kullanım:** Arşivlenmiş uygulama/sunucu logları, dışa aktarılmış denetim kayıtları, password dictionary ve wordlist dosyaları, JSONL/NDJSON kayıtları, trace çıktıları ve diğer satır tabanlı büyük metinler.

**Kapsam dışı:** Canlı log takibi, uzak sunucudan log toplama, dosya düzenleme ve JSON alanı bazlı yapılandırılmış analiz.

## Öne çıkanlar

- Sabit ve düşük RAM kullanımıyla kesintisiz sanal kaydırma.
- Her görünür satır için byte konumu, byte uzunluğu ve satır bilgisi.
- İleri/geri birebir arama ve disk tabanlı **Tümünü Tara**.
- Bütün dosya için kesin eşleşme sayısı ve sayfalı sonuç konumları.
- İsteğe bağlı satır navigasyon indeksi ve **Satıra git**.
- Yer imleri, konum geçmişi ve son oturuma dönme.
- Normal, görünmez karakter ve hex görünümleri.
- Aşırı uzun satırları güvenli biçimde inceleme.
- Anlık hız, taranan miktar, kalan süre ve darboğaz bilgisi.
- Kurulum gerektirmez ve kaynak dosyayı değiştirmez.

## Diller

Varsayılan dil İngilizcedir. Arayüz yeniden başlatılmadan değişir ve seçim sonraki açılışta hatırlanır:

- English
- Türkçe
- Español
- Français
- Deutsch
- Português (Brasil)

## Çalıştırma

1. Son GitHub Release içinden `WordlistViewer.exe` dosyasını indirin veya bu paketteki `release/WordlistViewer.exe` dosyasını kullanın.
2. 64-bit Windows'ta çalıştırın.
3. **Open File… / Dosya Aç…** düğmesini kullanın ya da `.txt`, `.lst`, `.dic`, `.wordlist`, `.log`, `.out`, `.trace`, `.jsonl` veya `.ndjson` dosyasını pencereye sürükleyin.

## Kaynaktan derleme

Windows 10/11 x64, .NET Framework 4.8 Developer Pack ve Windows PowerShell 5.1 veya üzeri gerekir.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

Hazır Visual Studio projesi için `WordlistViewer.csproj` dosyasını açabilirsiniz. Ayrıntılar için [İngilizce README](README.md), çeviri eklemek için [yerelleştirme rehberi](docs/LOCALIZATION.md) kullanılabilir.

Proje [MIT Lisansı](LICENSE) ile paylaşılır.

## Projeyi destekleyin

Wordlist & Log Viewer size zaman kazandırıyorsa geliştirme çalışmalarını isteğe bağlı bir kripto bağışıyla destekleyebilirsiniz:

- **Varlık:** Yerel USDC
- **Ağ:** Base Mainnet (zincir kimliği `8453`)
- **Adres:** [`0x82fc184297F6d4Ad845173Ab488F0845f9baE702`](https://basescan.org/address/0x82fc184297F6d4Ad845173Ab488F0845f9baE702)

Yalnızca Base ağı üzerinden yerel USDC gönderin ve transferi onaylamadan önce ağı ve adresin tamamını doğrulayın. Kripto transferleri normalde geri alınamaz; uyumsuz bir varlık veya ağ kullanılması kayba neden olabilir. Bağışlar gönüllüdür ve destek, özellik önceliği, hizmet veya garanti satın almaz.

