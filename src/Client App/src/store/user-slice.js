import { createSlice, current } from '@reduxjs/toolkit';
import {idle, loading, succeeded, failed} from '../utils/stateStatus';

const initialState = {
    status: idle,
    userInfo: {}
}

const userSlice = createSlice({

    name: 'user',
    initialState: initialState,
    reducers: {
        start_loading: (state) => {
            return {
                ...state,
                status: loading
            }
        },
        load_user_success: (state, action) => {

            return {
                ...state,
                status: succeeded,
                userInfo: action.payload
            }
        },
        register_user_success: (state) => {
            return {
                ...state,
                status: succeeded
            }
        },
        update_user_success: (state, action) => {

            var user = current(state.userInfo).find(item => item.id === action.payload.data.id);
            const index = current(state.userInfo).indexOf(user);

            var temp = [...current(state.userInfo)]
            console.log(action.payload.data)
            console.log(temp)
            temp.splice(index, 1, action.payload.data);


            return {
                ...state,
                status: succeeded,
                userInfo: temp

            }
        },
        update_user_password_success: (state) => {
            return {
                ...state,
                status: succeeded
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

export const userActions = userSlice.actions;

export default userSlice.reducer;