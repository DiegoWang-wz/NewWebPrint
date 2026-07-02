using AutoMapper;
using NewWebPrint.DataDtos;
using NewWebPrint.Models;

namespace NewWebPrint.Utilities;

public class AutoMapperSetting : Profile
{
    public AutoMapperSetting()
    {
        // --- 原有逻辑 ---
        CreateMap<byte, bool>().ConvertUsing(src => src == 1);
        CreateMap<bool, byte>().ConvertUsing(src => src ? (byte)1 : (byte)0);
        
        CreateMap<ProductTaskModel, ProductTaskDto>().ReverseMap();

        // --- 原有其他 DTO 映射 ---
        // CORD 侧唛列表
        CreateMap<CordListEntity, CordListDto>();
        CreateMap<CordListDto, CordListEntity>()
            .ForMember(dest => dest.Id, opt => opt.Ignore());
    }
}