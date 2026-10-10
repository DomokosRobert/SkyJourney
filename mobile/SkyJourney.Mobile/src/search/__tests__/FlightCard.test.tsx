import { fireEvent, render, screen } from '@testing-library/react-native';

import { offer } from '@/test/fixtures';
import { FlightCard } from '../FlightCard';

describe('FlightCard', () => {
  it('shows local times, overnight arrival, price and scarcity', async () => {
    const onPress = jest.fn();
    await render(
      <FlightCard
        offer={offer({ dep: '22:30', arrivalLocal: '2026-11-03T01:10:00+00:00', price: 89.5, seatsLeft: 3 })}
        onPress={onPress}
      />,
    );

    expect(screen.getByText('22:30')).toBeOnTheScreen();
    expect(screen.getByText(' +1')).toBeOnTheScreen();
    expect(screen.getByText('€89.50')).toBeOnTheScreen();
    expect(screen.getByText('Only 3 seats left')).toBeOnTheScreen();

    await fireEvent.press(screen.getByRole('button'));
    expect(onPress).toHaveBeenCalled();
  });

  it('is disabled when sold out', async () => {
    const onPress = jest.fn();
    await render(<FlightCard offer={offer({ price: null })} onPress={onPress} />);

    expect(screen.getByText('Sold out')).toBeOnTheScreen();
    await fireEvent.press(screen.getByRole('button'));
    expect(onPress).not.toHaveBeenCalled();
  });
});
