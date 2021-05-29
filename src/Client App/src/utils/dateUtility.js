export const getLocalDate = (date) => {
    const [localDate, localTime] = new Date(date).toLocaleString().split(',');
    return `${localTime}, ${localDate}`;
}