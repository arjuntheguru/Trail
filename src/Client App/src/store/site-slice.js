import { createSlice, current } from '@reduxjs/toolkit';

const initialState = {
    isLoading: false,
    siteInfo: {}
}

const siteSlice = createSlice({

    name: 'site',
    initialState: initialState,
    reducers: {
        start_loading: (state) => {
            return {
                ...state,
                isLoading: true
            }
        },
        load_site_success: (state, action) => {

            return {
                ...state,
                isLoading: false,
                siteInfo: { data: action.payload.data, count: action.payload.totalRecords }
            }
        },
        add_site_success: (state) => {
            return {
                ...state,
                isLoading: false                
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
                isLoading: false,
                siteInfo: updatedState

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

export const siteActions = siteSlice.actions;

export default siteSlice.reducer;