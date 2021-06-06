import { createSlice, current } from '@reduxjs/toolkit';
import {idle, loading, succeeded, failed} from '../utils/stateStatus';

const initialState = {
    status : idle,
    companyInfo: {}
}

const companySlice = createSlice({

    name: 'company',
    initialState: initialState,
    reducers: {
        start_loading: (state) => {
            return {
                ...state,
                status : loading
            }
        },
        load_company_success: (state, action) => {

            return {
                ...state,
                status : succeeded,
                companyInfo: { data: action.payload.data, count: action.payload.totalRecords }
            }
        },
        add_company_success: (state) => {
            return {
                ...state,
                status : succeeded                
            }
        },
        update_company_sucess: (state, action) => {

            var company = current(state.companyInfo.data).find(item => item.id === action.payload.data.id);
            const index = current(state.companyInfo.data).indexOf(company);

            var temp = [...current(state.companyInfo.data)]
            console.log(action.payload.data)
            console.log(temp)
            temp.splice(index, 1, action.payload.data);

            var updatedState = {
                
                ...state.companyInfo,
                data: temp,

            }

            return {
                ...state,
                status : succeeded,
                companyInfo: updatedState
            }
        },
        error: (state) => {
            return {
                ...state,
                status : failed 
            }
        },
        reset: () => initialState
    }

});

export const companyActions = companySlice.actions;

export default companySlice.reducer;