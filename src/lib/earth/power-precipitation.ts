export const POWER_RAIN_UNIT = "mm/month";

/** Convert POWER's monthly mean daily rate to a calendar-month total in mm. */
export function precipitationTotal(rate: number, year: number, month: number) {
  return rate * new Date(Date.UTC(year, month, 0)).getUTCDate();
}
