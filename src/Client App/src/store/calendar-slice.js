import { createSlice } from '@reduxjs/toolkit';
import {idle, loading, succeeded, failed} from '../utils/stateStatus';

const initialState = {
    status : idle,
    calendarInfo: {
        data: {}       
    },
    // minorStatus: idle
}

const calendarSlice = createSlice({

    name: 'calendar',
    initialState: initialState,
    reducers: {
        start_loading: (state) => {
            return {
                ...state,
                status: loading
            }
        },
        update_calendar_success: (state, action) => {

            return {
                ...state,
                status : succeeded,
                calendarInfo: {
                    ...state.calendarInfo,
                    data: action.payload.data               
                }
            }
        },
        load_calendar_success: (state, action) => {

            return {
                ...state,
                status: succeeded,
                calendarInfo: {
                    ...state.calendarInfo,
                    data: action.payload.data                   
                }
            }
        },        
        error: (state) => {
            return {
                ...state,
                status: failed
            }
        },
        reset: () => initialState
    }

});

export const calendarActions = calendarSlice.actions;

export default calendarSlice.reducer;