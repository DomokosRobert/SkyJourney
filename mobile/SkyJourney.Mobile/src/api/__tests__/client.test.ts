import { apiRequest, ApiError, setAuthHooks, toQueryString } from '../client';

jest.mock('../config', () => ({ API_BASE_URL: 'http://api.test' }));

const fetchMock = jest.fn();
globalThis.fetch = fetchMock as unknown as typeof fetch;

function jsonResponse(status: number, body: unknown) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

afterEach(() => {
  fetchMock.mockReset();
  setAuthHooks(null);
});

describe('apiRequest', () => {
  it('returns parsed JSON for successful responses', async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { iataCode: 'BUD' }));

    await expect(apiRequest('/api/v1/airports/BUD')).resolves.toEqual({ iataCode: 'BUD' });
    expect(fetchMock).toHaveBeenCalledWith('http://api.test/api/v1/airports/BUD', expect.objectContaining({ method: 'GET' }));
  });

  it('maps Problem Details to ApiError', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(400, {
        title: 'One or more validation errors occurred.',
        status: 400,
        code: 'validation_failed',
        errors: { origin: ['Origin must be a 3-letter IATA code.'] },
        correlationId: 'abc',
      }),
    );

    const error = (await apiRequest('/x').catch((e: unknown) => e)) as ApiError;
    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(400);
    expect(error.code).toBe('validation_failed');
    expect(error.fieldErrors.origin).toEqual(['Origin must be a 3-letter IATA code.']);
    expect(error.correlationId).toBe('abc');
  });

  it('reports transport failures as network errors', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Network request failed'));

    const error = (await apiRequest('/x').catch((e: unknown) => e)) as ApiError;
    expect(error.isNetworkError).toBe(true);
    expect(error.code).toBe('network_error');
  });

  it('attaches the bearer token and retries once after a successful refresh', async () => {
    let token = 'old';
    setAuthHooks({
      getAccessToken: () => token,
      refreshAfterUnauthorized: async () => {
        token = 'new';
        return true;
      },
    });
    fetchMock.mockResolvedValueOnce(jsonResponse(401, { status: 401 })).mockResolvedValueOnce(jsonResponse(200, { ok: true }));

    await expect(apiRequest('/me')).resolves.toEqual({ ok: true });
    expect(fetchMock.mock.calls[0][1].headers.Authorization).toBe('Bearer old');
    expect(fetchMock.mock.calls[1][1].headers.Authorization).toBe('Bearer new');
  });

  it('sends JSON bodies', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(apiRequest('/x', { method: 'POST', body: { a: 1 } })).resolves.toBeUndefined();
    const init = fetchMock.mock.calls[0][1];
    expect(init.body).toBe('{"a":1}');
    expect(init.headers['Content-Type']).toBe('application/json');
  });
});

describe('toQueryString', () => {
  it('skips empty values and encodes the rest', () => {
    expect(toQueryString({ origin: 'BUD', returnDate: undefined, q: 'São Paulo', n: 0, x: '' })).toBe(
      'origin=BUD&q=S%C3%A3o%20Paulo&n=0',
    );
  });
});
