import { createSlice, current } from '@reduxjs/toolkit';
import {idle, loading, succeeded, failed} from '../utils/stateStatus';

const initialState = {
    
    status: idle,
    siteInfo: {},
    siteById: {},
    recentGETMethod : null
}

const siteSlice = createSlice({

    name: 'site',
    initialState: initialState,
    reducers: {
        start_loading: (state) => {
            return {
                ...state,
                status: loading
            }
        },
        load_site_success: (state, action) => {

            return {
                ...state,
                status: succeeded,
                siteInfo: { data: action.payload.data, count: action.payload.totalRecords },
                recentGETMethod : action.payload.method
            }
        },
        load_siteById_success: (state, action) => {

            return {
                ...state,    
                status: succeeded,            
                siteById : action.payload.data
            }
        },
        add_site_success: (state) => {
            return {
                ...state,
                status: succeeded                
            }
        },
        update_site_sucess: (state, action) => {

            var site = current(state.siteInfo.data).find(item => item.id === action.payload.data.id);
            const index = current(state.siteInfo.data).indexOf(site);

            var temp = [...current(state.siteInfo.data)]
            console.log(action.payload.data)
            console.log(temp)
            temp.splice(index, 1, action.payload.data);

            var updatedState = {
                
                ...state.siteInfo,
                data: temp,

            }

            return {
                ...state,
                status: succeeded,
                siteInfo: updatedState

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

export const siteActions = siteSlice.actions;

export default siteSlice.reducer;