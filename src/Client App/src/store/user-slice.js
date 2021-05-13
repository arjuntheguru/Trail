import { createSlice, current } from '@reduxjs/toolkit';

const initialState = {
    isLoading: true,
    userInfo: {}
}

const userSlice = createSlice({

    name: 'user',
    initialState: initialState,
    reducers: {
        start_loading: (state) => {
            return {
                ...state,
                isLoading: true
            }
        },
        load_user_success: (state, action) => {

            return {
                ...state,
                isLoading: false,
                userInfo: action.payload
            }
        },
        register_user_success: (state) => {
            return {
                ...state,
                isLoading: false
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
                isLoading: false,
                userInfo: temp

            }
        },
        update_user_password_success: (state) => {
            return {
                ...state,
                isLoading: false
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

export const userActions = userSlice.actions;

export default userSlice.reducer;