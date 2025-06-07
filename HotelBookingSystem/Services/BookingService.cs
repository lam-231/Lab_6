using HotelBookingSystem.Models;
using HotelBookingSystem.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HotelBookingSystem.Services
{
    public class BookingService : IBookingService
    {
        private readonly IBookingRepository _repository;
        private readonly ILogger _logger;
        private readonly IAvailabilityStrategy _availabilityStrategy;

        public BookingService(
            IBookingRepository repository,
            ILogger logger,
            IAvailabilityStrategy availabilityStrategy)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _availabilityStrategy = availabilityStrategy ?? throw new ArgumentNullException(nameof(availabilityStrategy));
        }

        public Booking CreateBooking(int roomId, int guestId, DateTime checkIn, DateTime checkOut)
        {
            ValidateBookingDates(checkIn, checkOut);

            var existing = _repository.GetAll();
            if (!_availabilityStrategy.IsRoomAvailable(roomId, checkIn, checkOut, existing))
            {
                _logger.LogError($"Failed booking: room {roomId} not available {checkIn:d}–{checkOut:d}");
                throw new InvalidOperationException("Room is not available");
            }

            var booking = new Booking
            {
                Id = GetNextBookingId(existing),
                RoomId = roomId,
                GuestId = guestId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut
            };

            _repository.Add(booking);
            _repository.Save();
            _logger.LogInfo($"Created booking ID {booking.Id} for room {roomId} from {checkIn:d} to {checkOut:d}");

            return booking;
        }

        public bool CancelBooking(int bookingId)
        {
            var existing = _repository.GetById(bookingId);
            if (existing == null)
            {
                _logger.LogError($"Cancel failed: no booking with ID {bookingId}");
                return false;
            }

            _repository.Delete(bookingId);
            _repository.Save();
            _logger.LogInfo($"Cancelled booking ID {bookingId}");
            return true;
        }

        public IEnumerable<Booking> GetAllBookings()
            => _repository.GetAll();

        public IEnumerable<Booking> GetBookingsForRoom(int roomId)
            => _repository.GetAll().Where(b => b.RoomId == roomId);

        public bool IsRoomAvailable(int roomId, DateTime from, DateTime to)
            => !_repository.GetAll().Any(b =>
                   b.RoomId == roomId &&
                   b.CheckInDate < to &&
                   from < b.CheckOutDate);

        public void UpdateBooking(Booking updated)
        {
            ValidateBookingDates(updated.CheckInDate, updated.CheckOutDate);
            if (HasConflict(updated))
            {
                _logger.LogError($"Update failed: booking conflict for ID {updated.Id}");
                throw new InvalidOperationException("Booking conflicts with existing reservation");
            }

            _repository.Update(updated);
            _repository.Save();
            _logger.LogInfo($"Updated booking ID {updated.Id}");
        }

        public IEnumerable<Booking> FilterBookings(DateTime? from = null, DateTime? to = null, int? roomId = null)
        {
            return _repository.GetAll().Where(b =>
                (!from.HasValue || b.CheckInDate >= from.Value) &&
                (!to.HasValue || b.CheckOutDate <= to.Value) &&
                (!roomId.HasValue || b.RoomId == roomId.Value));
        }

        public IEnumerable<Room> GetAvailableRooms()
            => _repository.GetRooms();


        private void ValidateBookingDates(DateTime checkIn, DateTime checkOut)
        {
            if (checkIn < DateTime.Today)
                throw new ArgumentException("Check-in date cannot be in the past");
            if (checkOut <= checkIn)
                throw new ArgumentException("Check-out date must be after check-in date");
        }

        private bool HasConflict(Booking booking)
        {
            return _repository.GetAll().Any(b =>
                b.Id != booking.Id &&
                b.RoomId == booking.RoomId &&
                b.CheckInDate < booking.CheckOutDate &&
                booking.CheckInDate < b.CheckOutDate);
        }

        private int GetNextBookingId(IEnumerable<Booking> existing)
        {
            return existing.Any() ? existing.Max(b => b.Id) + 1 : 1;
        }
    }
}
