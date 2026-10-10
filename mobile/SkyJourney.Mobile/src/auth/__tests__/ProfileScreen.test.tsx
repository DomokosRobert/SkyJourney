import { fireEvent, screen, waitFor } from '@testing-library/react-native';

import { authApi } from '@/api/auth';
import ProfileScreen from '@/app/(tabs)/profile';
import { TRAVELLER } from '@/test/fixtures';
import { renderWithProviders } from '@/test/renderWithProviders';

jest.mock('expo-router', () => ({ router: { push: jest.fn(), navigate: jest.fn() } }));

describe('ProfileScreen', () => {
  afterEach(() => jest.restoreAllMocks());

  it('updates the name and keeps the session in sync', async () => {
    const updateMe = jest
      .spyOn(authApi, 'updateMe')
      .mockImplementation(async (body) => ({ ...TRAVELLER, ...body }));
    const { manager } = await renderWithProviders(<ProfileScreen />);

    await fireEvent.press(await screen.findByRole('button', { name: 'Edit name' }));
    await fireEvent.changeText(screen.getByLabelText('First name'), ' Augusta ');
    await fireEvent.press(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Augusta Lovelace')).toBeOnTheScreen();
    expect(updateMe).toHaveBeenCalledWith({ firstName: 'Augusta', lastName: 'Lovelace' });
    await waitFor(() => expect(manager.getState()).toMatchObject({ user: { firstName: 'Augusta' } }));
  });

  it('validates the name before calling the API', async () => {
    const updateMe = jest.spyOn(authApi, 'updateMe');
    await renderWithProviders(<ProfileScreen />);

    await fireEvent.press(await screen.findByRole('button', { name: 'Edit name' }));
    await fireEvent.changeText(screen.getByLabelText('Last name'), '  ');
    await fireEvent.press(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Enter your last name.')).toBeOnTheScreen();
    expect(updateMe).not.toHaveBeenCalled();
  });
});
