GAMELOG DESKTOP 1.4 — THỜI HẠN KEY, THÔNG BÁO VÀ CHẶN CHI PHÍ

CẬP NHẬT
Trong app đang dùng: Kiểm tra cập nhật → Cập nhật từ file ZIP → chọn GameLogDesktop-Windows-x64-v1.4.zip.
App cập nhật tại thư mục đang dùng, kiểm tra hash và giữ desktop-data theo cơ chế updater.

THEO DÕI KEY
1. Việt hoá → chọn Metro 2033 Redux → Chuẩn bị tự dịch.
2. Nhập API key vào ô che ký tự, bấm Ghi nhận key · 30 ngày.
3. Bộ đếm hiển thị ngày/giờ/phút/giây còn lại và ngày hết hạn theo giờ Việt Nam.
4. Mốc được lưu từ lần ghi nhận đầu tiên. Nhập lại cùng key không đặt lại 30 ngày; key khác có mốc riêng.
5. Khi còn dưới hoặc bằng 3 ngày, thông báo cảnh báo xuất hiện một lần. Khi hết 30 ngày, app chặn dịch bằng key này và tạo thông báo hết hạn.
6. App kiểm tra khi đang mở và lần mở kế tiếp. App đóng thì không có thông báo chạy nền ngoài Windows.

Đây là mốc 30 ngày theo yêu cầu của bạn, không phải thông tin hết hạn xác minh từ OpenAI/nhà cung cấp key.
Key gốc chỉ giữ trong phiên cửa sổ; sau khi đóng phải nhập lại. notifications.json chỉ lưu dấu nhận dạng SHA256, thời điểm và thông báo/nhật ký token, không lưu key gốc.

THÔNG BÁO
Nút Thông báo (số chưa đọc) ở đầu app mở tab Thông báo.
Tab ghi cảnh báo key, tác vụ dịch bị chặn, dịch hoàn tất và số token/chi phí có trong phản hồi.
Bấm Đánh dấu tất cả đã đọc để bỏ số chưa đọc. Thông báo và trạng thái đọc được lưu qua lần mở app.

CHI PHÍ TRƯỚC KHI DỊCH
1. Đọc câu tiếng Anh từ Metro.
2. Bấm Tính token và chi phí. App đọc giá standard hiện tại từ trang model chính thức và dùng endpoint đếm token của OpenAI cho cả câu, chỉ dẫn và schema.
3. Xem token đầu vào, token đầu ra dự kiến, trần token đầu ra 10.000 và chi phí trần dự phòng.
4. Nếu trần dự phòng vượt 1 USD, không gửi yêu cầu dịch và ghi thông báo. Giới hạn áp dụng cho từng lần bấm dịch, không áp dụng tổng 30 ngày.
5. Nếu không xác minh được giá, không đếm được token, key hết hạn hoặc model chưa có bộ đọc giá, app chặn dịch.
6. Ước tính có hiệu lực 5 phút. Đổi key/model/câu hoặc hết thời gian cần tính lại.
7. Sau khi xem chi phí, bấm Dịch 20 câu. Mỗi lô kế tiếp cần tính lại. Chưa có nút dịch cả game.

Trần dự phòng tính bằng token đầu vào cộng 5% và 256 token dự phòng, cùng toàn bộ mức token đầu ra tối đa. Không lấy riêng số token dự kiến để quyết định ngưỡng.
Chỉ hỗ trợ model gpt-4.1-mini trong bộ đọc giá hiện tại, không đoán giá model khác.
Trong phản hồi có usage, app tính chi phí theo số token và giá standard đã xác minh; con số này chưa trừ ưu đãi cache, không thay thế hoá đơn/dashboard OpenAI.
Khi mạng bị ngắt hoặc huỷ sau khi gửi, nhà cung cấp có thể đã xử lý/tính phí. App không tự gửi lại; phần dự phòng còn giữ trong nhật ký nếu chưa xác định được usage.
Không xác định được chi phí/token sẽ chặn gọi dịch. Bước đếm token vẫn gửi các câu của lô tới endpoint đếm của OpenAI.

KIỂM THỬ
Kiểm tra mốc thời gian bằng đồng hồ giả lập; kiểm tra các đường chặn bằng HTTP giả lập, không dùng API key thật.
Đã kiểm tra bộ đọc giá với trang OpenAI thật. Chưa gọi endpoint dịch hoặc đếm token bằng API key thật.
Phần font, đóng gói và tự thay file game vẫn chưa hoàn tất; nhập ZIP vẫn chỉ kiểm tra cấu trúc.

TÀI LIỆU OPENAI CHÍNH THỨC
https://developers.openai.com/api/docs/guides/token-counting
https://developers.openai.com/api/docs/models/gpt-4.1-mini
