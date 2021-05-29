import { createSlice } from '@reduxjs/toolkit';

const initialState = {
    isLoading: false,
    calendarInfo: {
        data: {},
        isLoaded: false
    }
}

const calendarSlice = createSlice({

    name: 'calendar',
    initialState: initialState,
    reducers: {
        start_loading: (state) => {
            return {
                ...state,
                isLoading: true
            }
        },
        update_calendar_success: (state, action) => {

            return {
                ...state,
                isLoading: false,
                calendarInfo: {
                    ...state.calendarInfo,
                    data: action.payload.data,
                    isLoaded: true
                }
            }
        },
        load_calendar_success: (state, action) => {

            return {
                ...state,
                isLoading: false,
                calendarInfo: {
                    ...state.calendarInfo,
                    data: action.payload.data,
                    isLoaded: true
                }
            }
        },        
        error: (state) => {
            return {
                ...state,
                isLoading: false
            }
        },
        reset: () => initialState
    }

});

export const calendarActions = calendarSlice.actions;

export default calendarSlice.reducer;