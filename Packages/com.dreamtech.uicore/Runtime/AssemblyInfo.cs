using System.Runtime.CompilerServices;

// Editor (xem trước trong Edit mode, bảng debug) và test đọc được trạng thái nội bộ của list ảo hoá mà không phải mở API công khai.
[assembly: InternalsVisibleTo("DreamTech.UICore.Editor")]
[assembly: InternalsVisibleTo("DreamTech.UICore.Tests")]
[assembly: InternalsVisibleTo("DreamTech.UICore.Tests.Runtime")]
