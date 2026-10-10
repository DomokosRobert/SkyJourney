import { fireEvent, render, screen } from '@testing-library/react-native';

import { seatMap } from '@/test/fixtures';
import { SeatMapView } from '../SeatMapView';

describe('SeatMapView', () => {
  it('announces each seat with its category, state and price', async () => {
    await render(<SeatMapView map={seatMap()} initials={{}} onSelect={jest.fn()} />);

    expect(screen.getByLabelText(/^Seat 1A, Front rows, available, €9\.99$/)).toBeOnTheScreen();
    expect(screen.getByLabelText('Seat 1B, Front rows, taken')).toBeDisabled();
  });

  it('shows the passenger initials on seats of this booking', async () => {
    const map = seatMap();
    map.seats[4] = { ...map.seats[4]!, availability: 'Selected', passengerId: 'p1' };

    await render(<SeatMapView map={map} initials={{ p1: 'AL' }} onSelect={jest.fn()} />);

    expect(screen.getByLabelText('Seat 2A, Standard, selected')).toHaveTextContent('AL');
  });

  it('reports taps on free seats but not on taken ones', async () => {
    const onSelect = jest.fn();
    await render(<SeatMapView map={seatMap()} initials={{}} onSelect={onSelect} />);

    await fireEvent.press(screen.getByLabelText('Seat 1B, Front rows, taken'));
    await fireEvent.press(screen.getByLabelText(/^Seat 2C, Standard, available/));

    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ seatNumber: '2C' }));
  });
});
