import { fireEvent, render, screen } from '@testing-library/react-native';

import { Button } from '../Button';

describe('Button', () => {
  it('calls onPress when enabled', async () => {
    const onPress = jest.fn();
    await render(<Button title="Search flights" onPress={onPress} />);

    await fireEvent.press(screen.getByRole('button', { name: 'Search flights' }));
    expect(onPress).toHaveBeenCalledTimes(1);
  });

  it('does not fire while loading', async () => {
    const onPress = jest.fn();
    await render(<Button title="Pay" onPress={onPress} loading />);

    await fireEvent.press(screen.getByRole('button'));
    expect(onPress).not.toHaveBeenCalled();
  });
});
