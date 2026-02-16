using Application.DTOs;
using AutoMapper;
using Domain.Entities;

namespace Application.Mappings;

/// <summary>
/// AutoMapper profile for entity to DTO mappings
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Player mappings
        CreateMap<Player, PlayerDto>()
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

        CreateMap<UpdatePlayerDto, Player>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

        // Transaction mappings
        CreateMap<Transaction, TransactionDto>()
            .ForMember(dest => dest.PlayerUsername, opt => opt.MapFrom(src => src.Player.Username))
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
            .ForMember(dest => dest.PaymentMethodName, opt => opt.MapFrom(src => src.PaymentMethod != null ? src.PaymentMethod.Name : null))
            .ForMember(dest => dest.ApprovedByUsername, opt => opt.MapFrom(src => src.ApprovedBy != null ? src.ApprovedBy.Username : null));

        CreateMap<CreateDepositDto, Transaction>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => Domain.Enums.TransactionType.Deposit))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => Domain.Enums.TransactionStatus.Pending));

        CreateMap<CreateWithdrawalDto, Transaction>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => Domain.Enums.TransactionType.Withdrawal))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => Domain.Enums.TransactionStatus.Pending));

        // PaymentMethod mappings
        CreateMap<PaymentMethod, PaymentMethodDto>()
            .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Type.ToString()));

        // Notification mappings
        CreateMap<Notification, NotificationDto>();
    }
}


