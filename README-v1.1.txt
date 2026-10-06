GAMELOG DESKTOP 1.1 — VIỆT HOÁ

CẬP NHẬT BẢN ĐANG DÙNG
1. Trong app đang dùng, bấm Sao lưu rồi đóng app.
2. Giải nén GameLogDesktop-Update-v1.1.zip.
3. Chép TẤT CẢ FILE trong thư mục Update-v1.1 vào ĐÚNG thư mục chứa GameLogDesktop.exe bạn đang mở trong E:\Desktop App. Chọn thay thế các file trùng tên.
4. Giữ nguyên thư mục desktop-data. Bản cập nhật không chứa thư mục này và không yêu cầu nhập lại dữ liệu.
5. Mở lại GameLogDesktop.exe, vào tab Việt hoá.

Bản đầy đủ GameLogDesktop-Windows-x64-v1.1.zip cũng có thể giải nén vào thư mục riêng, nhưng muốn giữ dữ liệu cần chép desktop-data từ thư mục đang dùng sang cạnh file exe mới.

TAB VIỆT HOÁ
Tự hiển thị game trong thư viện, loại phần mềm, sắp theo giờ chơi giảm dần và có STT. Hai nguồn: The Red Team và Cánh Cụt Team. Hai cột Tự Dịch Việt Hóa / Nhập File Việt Hóa ghi Làm sau.
Kèm danh mục tra cứu ngày 06/10/2026: 266 mục The Red Team và 176 mục Cánh Cụt Team, có kiểm tra nền tảng PC. Bấm Cập nhật từ hai nguồn để tải danh mục mới và kiểm tra từng trang game khớp thư viện của bạn.
Chọn dòng để xem tên trên nguồn, ghi chú, liên kết và thời gian kiểm tra. Hai nút Mở nguồn mở đúng trang của game được chọn, hoặc mở danh mục nếu chưa có kết quả.

TRẠNG THÁI
Có bản dịch · Free: nguồn xác nhận miễn phí.
Có bản dịch · Trả phí: bao gồm trải nghiệm sớm yêu cầu ủng hộ/donate. Không dùng giá sản phẩm 0 ₫ để suy ra miễn phí; đọc trạng thái tại trang nguồn.
Không có bản dịch: chưa thấy tên game khớp trong danh mục PC của nguồn ở thời điểm kiểm tra. Không khẳng định tuyệt đối rằng bản dịch không tồn tại; có thể tìm thủ công nếu nguồn dùng tên khác.
Chưa kiểm tra / Chưa rõ phí: giữ trạng thái chưa xác minh khi danh mục hoặc trạng thái không đọc được. Không biến lỗi mạng thành kết luận không có bản dịch.
Có đối chiếu Steam AppID nếu nguồn cung cấp và một số ánh xạ tên phiên bản cụ thể; không ghép theo tiền tố để tránh nhầm DLC/phần tiếp theo.

DỮ LIỆU VÀ PHẠM VI
Cache ở desktop-data\translations.json, được sao lưu cùng các JSON khác. Danh mục gốc đóng gói trong translation-catalog.json. App cập nhật khi bấm nút, không tự tải/cài/gỡ bản dịch hoặc sửa file game.
Danh mục công khai đã được tra cứu và chức năng cập nhật đã thử trực tiếp với hai website. Môi trường thực hiện không đọc được ổ E của bạn nên thư viện thật sẽ được đối chiếu khi app mới chạy tại máy bạn; không có dữ liệu thử trong bản phát hành.
Nguồn có thể thay đổi trạng thái và phiên bản hỗ trợ; liên kết trang gốc luôn có để kiểm tra lại.

NGUỒN
https://theredteam.vn/viet-hoa
https://canhcutteam.com/games/
https://canhcutteam.com/platform/pc/
https://canhcutteam.com/faq/

MÃ NGUỒN
TranslationModels.cs: cấu trúc dữ liệu Việt hoá.
TranslationService.cs: đọc danh mục, ghép tên/AppID, phân loại trạng thái, cache.
MainWindow.Translations.cs: điều khiển tab Việt hoá.
MainWindow.xaml / App.xaml: giao diện dùng chung.
Mã nguồn không chứa dữ liệu thư viện của bạn hoặc API key.
