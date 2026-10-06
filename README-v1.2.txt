GAMELOG DESKTOP 1.2 — CẬP NHẬT VÀ SHORTCUT

NÂNG CẤP BẢN ĐANG DÙNG (MỘT LẦN ĐỂ THÊM NÚT MỚI)
1. Trong app cũ, bấm Sao lưu.
2. Giải nén GameLogDesktop-Installer-v1.2.zip.
3. Nhấp đúp Cai-cap-nhat.bat.
4. Chọn đúng GameLogDesktop.exe đang dùng trong E:\Desktop App (có thể nằm trong thư mục con).
Bộ cài xác minh các file, yêu cầu app cũ đóng, sao lưu JSON sau khi app đóng, cập nhật rồi mở lại. Không cần nhập lại dữ liệu. Nếu bạn chưa dùng bộ runtime tương ứng, bộ cài sẽ yêu cầu dùng bản đầy đủ thay vì chép file không khớp.

TẠO SHORTCUT DESKTOP
Trong app mới: Chức năng → Tạo shortcut ngoài Desktop.
Shortcut GameLog Desktop sẽ trỏ tới exe tại thư mục đang dùng và đặt đúng thư mục làm việc. Không kéo riêng exe ra Desktop. Nếu Desktop đang có shortcut cùng tên của chương trình khác, app tạo tên khác để giữ shortcut cũ.
Cách làm thủ công: chuột phải GameLogDesktop.exe → Show more options (Hiển thị thêm tuỳ chọn) → Send to (Gửi tới) → Desktop (create shortcut). Sau này cập nhật ngay trong cùng thư mục thì shortcut vẫn dùng được.

KIỂM TRA CẬP NHẬT
Nút Kiểm tra cập nhật nằm ở đầu app và trong menu Chức năng.
Chưa có nguồn phát hành trực tuyến mặc định: bạn có thể chọn Cập nhật từ file ZIP để app tự xử lý gói mới có update-manifest.json. Các ZIP v1.0/v1.1 cũ chưa có manifest nên không dùng cho nút này. Bản 1.2 và các bản phát hành sau có manifest.
App sẽ chỉ cài phiên bản mới hơn, kiểm tra SHA256 từng file, sao lưu dữ liệu, đóng để thay file, kiểm tra bản mới khởi động, rồi dọn file tạm. Nếu thay file/khởi động thất bại, app cố khôi phục bản cũ. Khi không thể khôi phục đầy đủ, giữ lại thư mục rollback để phục hồi thủ công.
Để một nút tải trực tuyến: cần đăng gói ZIP và update-feed.json ở nguồn HTTPS ổn định (ví dụ GitHub Releases), rồi dán link update-feed.json vào hộp cập nhật. Link được lưu cho những lần sau. Gói tải phải khớp SHA256 và phiên bản nguồn thông báo. Chưa có nguồn thực tế nên bản này không tự tuyên bố đã kiểm tra được phiên bản mới nhất trên mạng.

DỌN FILE
Chỉ xoá file thuộc manifest của bản cũ, không còn trong bản mới, và vẫn nguyên trạng theo SHA256. File bạn tự thêm hoặc đã sửa được giữ nguyên. Thư mục desktop-data, nhật ký và các bản sao lưu không bị xoá. Các file cũ từ trước khi có manifest cũng được giữ để tránh nhận nhầm file của bạn.
Nếu không thể hoàn tất khôi phục, thư mục .gamelog-update-* chứa bản sao chương trình được giữ nguyên; không xoá nó trước khi phục hồi.

BẢN ĐẦY ĐỦ
GameLogDesktop-Windows-x64-v1.2.zip dùng để chạy trên máy mới hoặc khi thiếu runtime. Giải nén toàn bộ; muốn thay thư mục app thì chép desktop-data cạnh exe mới để giữ nhật ký. Không chép dữ liệu thử từ thư mục work.

PHÁT HÀNH SAU NÀY (MÃ NGUỒN)
Build bằng SDK .NET 8:
dotnet publish GameLogDesktop.csproj -c Release -r win-x64 --self-contained true -o publish
Chạy Generate-Release.ps1 với PublishFolder, Version, ZipPath và PackageUrl để tạo manifest, ZIP và feed. PublishFolder phải sạch, không chứa dữ liệu cá nhân. Đăng ZIP và file .feed.json lên HTTPS; có thể đổi tên feed thành update-feed.json. Feed gồm Product=GameLogDesktop, Version, PackageUrl và PackageSha256.
