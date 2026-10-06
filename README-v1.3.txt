GAMELOG DESKTOP 1.3 — BẢN THỬ NGHIỆM VIỆT HOÁ METRO

ĐÃ CÓ
- Trong Việt hoá: chọn Metro 2033 Redux, bấm Chuẩn bị tự dịch.
- Tự tìm thư mục game qua Steam; nếu không tìm được, Chọn thư mục game.
- Đọc câu tiếng Anh từ Metro: chỉ đọc archive gốc, giải nén vào bộ nhớ.
- Đối chiếu bảng ký tự và báo dấu tiếng Việt còn thiếu.
- Nhập OpenAI API key trong ô che ký tự. Key chỉ giữ trong phiên cửa sổ, không lưu vào JSON/sao lưu.
- Dịch thử 20 câu: gửi các câu của lô này tới OpenAI Responses API, model mặc định gpt-4.1-mini. API có phí riêng. Nút lần tiếp theo lấy tối đa 20 câu chưa dịch tiếp theo; chưa có nút dịch cả game.
- Kiểm tra mã câu, biến, thẻ theo thứ tự và số dòng; lô lỗi chưa được nhập vào bản nháp. Không tự gửi lại lô lỗi để tránh phí lặp.
- Xuất bản nháp JSON để lưu kết quả trước khi đóng cửa sổ. Chưa tự lưu hoặc nạp lại bản nháp.
- Kiểm tra file Việt hoá: chọn ZIP, kiểm tra đường dẫn, tên trùng, giới hạn giải nén, định dạng và chữ ký MZ của chương trình Windows đổi đuôi.

CHƯA CÓ
- Chưa tạo font tiếng Việt, ghi LNG, đóng gói mod hoặc tự thay file game.
- ZIP chưa được quét mã độc, xác minh phiên bản/tính tương thích hoặc cài đặt; bước này không chứng nhận gói an toàn.
- Chưa có bộ đọc tự dịch cho game khác.
- Chưa chạy bản dịch trong game hoặc thử API trả phí thật. Luồng API kiểm thử bằng phản hồi giả lập.
- Số mục trong tài nguyên có thể gồm nội dung dùng chung/không dùng ở bản game này; chưa lọc chính xác theo từng cảnh.

KIỂM TRA BẢN METRO THỰC TẾ
Steam AppID 286690, build 3790582, mục lục 81.689 entry, 29 archive.
Đọc được 27.038 mục tiếng Anh từ content/localization/stable_us.lng.
Bảng ký tự gốc thiếu nhiều dấu Việt. Không được chỉ đổi text rồi thay vào archive gốc.
Bộ đọc giữ thứ tự và tên khoá gốc; ID lô dịch riêng biệt giúp không làm mất các khoá trùng trong tài nguyên.

CẬP NHẬT APP
Trong bản 1.2: Kiểm tra cập nhật → Cập nhật từ file ZIP → chọn GameLogDesktop-Windows-x64-v1.3-preview.zip.
ZIP có manifest và hash, cập nhật tại thư mục đang dùng và giữ dữ liệu theo cơ chế bản 1.2.
Không chép riêng EXE hoặc DLL ra ngoài thư mục runtime. Không ghi đè thủ công desktop-data.

NGUỒN TÀI LIỆU
https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses
https://developers.openai.com/api/docs/models/gpt-4.1-mini
https://github.com/zerlkung/Metro-Redux-Tools
https://github.com/sisizanohito/MetroRedux_JapaneseMod
Các nguồn chỉ dùng đối chiếu định dạng; không tải/chạy trình cài mod hoặc sao chép bản dịch của các nhóm này.
